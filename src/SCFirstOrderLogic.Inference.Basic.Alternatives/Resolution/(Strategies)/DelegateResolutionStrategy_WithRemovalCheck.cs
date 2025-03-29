// Copyright (c) 2021-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic.Inference.Basic.InternalUtilities;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// <para>
/// A basic resolution strategy that just filters and prioritises clause resolutions using given delegates.
/// </para>
/// <para>
/// Notes:
/// </para>
/// <list type="bullet">
/// <item/>Handles subsumption checks occuring in clause stores by checking that both input clauses are still present
/// in the store before pulling a resolution from the queue.
/// </list>
/// </summary>
public class DelegateResolutionStrategy_WithRemovalCheck : IResolutionStrategy
{
    private readonly IKnowledgeBaseClauseStore clauseStore;
    private readonly Func<ClauseResolution, bool> filter;
    private readonly Comparison<ClauseResolution> priorityComparison;
    
    /// <summary>
    /// Initialises a new instance of the <see cref="DelegateResolutionStrategy_WithRemovalCheck"/> class.
    /// </summary>
    /// <param name="clauseStore">The clause store to use.</param>
    /// <param name="filter">
    /// A delegate to use to filter the pairs of clauses to be queued for a resolution attempt.
    /// A true value indicates that the pair should be enqueued. See the <see cref="Filters"/>
    /// inner class for some useful examples.
    /// </param>
    /// <param name="priorityComparison">
    /// A delegate to use to compare the pairs of clauses to be queued for a resolution attempt.
    /// See the <see cref="PriorityComparisons"/> inner class for some useful examples.
    /// </param>
    public DelegateResolutionStrategy_WithRemovalCheck(
        IKnowledgeBaseClauseStore clauseStore,
        Func<ClauseResolution, bool> filter,
        Comparison<ClauseResolution> priorityComparison)
    {
        this.clauseStore = clauseStore;
        this.filter = filter;
        this.priorityComparison = priorityComparison;
    }

    /// <inheritdoc/>
    public Task<bool> AddClauseAsync(CNFClause clause, CancellationToken cancellationToken)
    {
        return clauseStore.AddAsync(clause, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IResolutionQueryStrategy> MakeQueryStrategyAsync(ResolutionQuery query, CancellationToken cancellationToken)
    {
        return new QueryStrategy(query, await clauseStore.CreateQueryStoreAsync(cancellationToken), filter, priorityComparison);
    }

    /// <summary>
    /// The strategy implementation for individual queries.
    /// </summary>
    private class QueryStrategy : IResolutionQueryStrategy
    {
        private readonly ResolutionQuery query;
        private readonly IQueryClauseStore clauseStore;
        private readonly Func<ClauseResolution, bool> filter;
        private readonly MaxPriorityQueue<ClauseResolution> priorityQueue;

        public QueryStrategy(
            ResolutionQuery query,
            IQueryClauseStore clauseStore,
            Func<ClauseResolution, bool> filter,
            Comparison<ClauseResolution> priorityComparison)
        {
            this.query = query;
            this.clauseStore = clauseStore;
            this.filter = filter;
            this.priorityQueue = new MaxPriorityQueue<ClauseResolution>(priorityComparison);
        }

        public bool IsQueueEmpty => !HasNextEffectiveQueuedResolution();

        public ClauseResolution DequeueResolution()
        {
            if (!HasNextEffectiveQueuedResolution())
            {
                throw new InvalidOperationException("Resolutions exhausted!");
            }

            return priorityQueue.Dequeue();
        }

        public void Dispose() => clauseStore.Dispose();

        public async Task EnqueueInitialResolutionsAsync(CancellationToken cancellationToken)
        {
            // Initialise the query clause store with the clauses from the negation of the query:
            foreach (var clause in query.NegatedQuerySentence.Clauses)
            {
                await clauseStore.AddAsync(clause, cancellationToken);
            }

            // Queue up initial clause pairings:
            // TODO-PERFORMANCE: potentially repeating a lot of work here. Some of this (i.e. clause pairings that are just
            // from the KB - not from the negated query sentence) will be repeated every query, and some will produce the same
            // resolvent as each other - note that we don't filter out such dupes. Some caching here would be useful - is this
            // in scope for this *simple* implementation?
            await foreach (var clause in clauseStore)
            {
                await foreach (var resolution in clauseStore.FindResolutions(clause, cancellationToken))
                {
                    // NB: Throwing away clauses returned by (an arbitrary) clause store obviously has a performance impact.
                    // Better to use a store that knows to not look for certain clause pairings in the first place.
                    // However, the purpose of this strategy implementation is demonstration, not performance, so this is fine.
                    if (filter(resolution))
                    {
                        priorityQueue.Enqueue(resolution);
                    }
                }
            }
        }

        public async Task EnqueueResolutionsAsync(CNFClause clause, CancellationToken cancellationToken)
        {
            // Check if we've found a new clause (i.e. something that we didn't know already).
            // Downside of using Add: clause store will encounter itself when looking for unifiers - not a big deal,
            // but a performance/simplicity tradeoff nonetheless.
            if (await clauseStore.AddAsync(clause, cancellationToken))
            {
                // This is a new clause, so find and queue up its resolutions.
                await foreach (var newResolution in clauseStore.FindResolutions(clause, cancellationToken))
                {
                    // NB: Throwing away clauses returned by (an arbitrary) clause store obviously has a performance impact.
                    // Better to use a store that knows to not look for certain clause pairings in the first place.
                    // However, the purpose of this strategy implementation is to be a basic example, not a high performance one, so this is fine.
                    if (filter(newResolution))
                    {
                        priorityQueue.Enqueue(newResolution);
                    }
                }
            }
        }

        // side-effect: consumes non-effective from queue. yeah, this is ugly code..
        private bool HasNextEffectiveQueuedResolution()
        {
            do
            {
                if (priorityQueue.Count == 0)
                {
                    return false;
                }

                var nextResolution = priorityQueue.Peek();

                // non-effective if either of the resolving clauses are no longer in the store (which will be because they are
                // subsumed by another clause since added). Potentially more efficient to remove resolutions from the queue as
                // the subsumed clauses are removed, but this would require more complexity in the queue itself to allow this.
                // For now, this approach will do.
                if (!clauseStore.ContainsAsync(nextResolution.Clause1).GetAwaiter().GetResult() || !clauseStore.ContainsAsync(nextResolution.Clause2).GetAwaiter().GetResult())
                {
                    priorityQueue.Dequeue(); // nb - no thread safety here, but that's fine for now at least
                }
                else
                {
                    return true;
                }
            }
            while (true);
        }
    }
}
