// Copyright (c) 2021-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
// NB: Not quite ready yet. In truth, I'm not completely sure that it's even trying to do the right thing.
// It was created to prove out the strategy abstraction more than anything else.
using SCFirstOrderLogic.Inference.Basic.InternalUtilities;
using System.Diagnostics;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// Resolution strategy that applies linear resolution.
/// </summary>
public class LinearResolutionStrategy_WithRemovalCheck : IResolutionStrategy
{
    private readonly IKnowledgeBaseClauseStore clauseStore;
    private readonly Comparison<ClauseResolution> priorityComparison;

    /// <summary>
    /// Initialises a new instance of the <see cref="LinearResolutionStrategy_WithRemovalCheck"/> class.
    /// </summary>
    /// <param name="clauseStore">The clause store to use.</param>
    public LinearResolutionStrategy_WithRemovalCheck(
        IKnowledgeBaseClauseStore clauseStore,
        Comparison<ClauseResolution> priorityComparison)
    {
        this.clauseStore = clauseStore;
        this.priorityComparison = priorityComparison;
    }

    /// <inheritdoc/>
    public async Task<bool> AddClauseAsync(CNFClause clause, CancellationToken cancellationToken)
    {
        return await clauseStore.AddAsync(clause, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IResolutionQueryStrategy> MakeQueryStrategyAsync(ResolutionQuery query, CancellationToken cancellationToken)
    {
        return new QueryStrategy(
            query,
            await clauseStore.CreateQueryStoreAsync(cancellationToken),
            await clauseStore.CreateQueryStoreAsync(cancellationToken),
            priorityComparison);
    }

    private class QueryStrategy : IResolutionQueryStrategy
    {
        private readonly ResolutionQuery query;
        private readonly IQueryClauseStore inputClauseStore;
        private readonly IQueryClauseStore intermediateClauseStore;
        private readonly MaxPriorityQueue<ClauseResolution> queue;

        public QueryStrategy(
            ResolutionQuery query,
            IQueryClauseStore inputClauseStore,
            IQueryClauseStore intermediateClauseStore,
            Comparison<ClauseResolution> priorityComparison)
        {
            this.query = query;
            this.inputClauseStore = inputClauseStore;
            this.intermediateClauseStore = intermediateClauseStore;
            this.queue = new MaxPriorityQueue<ClauseResolution>(priorityComparison);
        }

        /// <inheritdoc />
        public bool IsQueueEmpty => !HasNextEffectiveQueuedResolution();

        /// <inheritdoc />
        public ClauseResolution DequeueResolution()
        {
            // todo: allow for removals from queue instead - or just dont
            if (!HasNextEffectiveQueuedResolution())
            {
                throw new InvalidOperationException("Resolutions exhausted!");
            }

            return queue.Dequeue();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            inputClauseStore.Dispose();
            intermediateClauseStore.Dispose();
        }

        /// <inheritdoc />
        public async Task EnqueueInitialResolutionsAsync(CancellationToken cancellationToken)
        {
            // Initialise the query clause store with the clauses from the negation of the query:
            foreach (var clause in query.NegatedQuerySentence.Clauses)
            {
                await inputClauseStore.AddAsync(clause, cancellationToken);
                await intermediateClauseStore.AddAsync(clause, cancellationToken);
            }

            // Queue up initial clause pairings:
            // TODO-PERFORMANCE-MAJOR: potentially repeating a lot of work here - could cache the results of pairings
            // of KB clauses with each other. Or at least don't keep re-attempting ones that we know fail.
            // Is this in scope for this *basic* implementation?
            await foreach (var clause in inputClauseStore)
            {
                await foreach (var resolution in inputClauseStore.FindResolutions(clause, cancellationToken))
                {
                    queue.Enqueue(resolution);
                }
            }
        }

        /// <inheritdoc />
        public async Task EnqueueResolutionsAsync(CNFClause clause, CancellationToken cancellationToken)
        {
            // Check if we've found a new clause (i.e. something that we didn't know already).
            // Downside of using Add: clause store will encounter itself when looking for unifiers - not a big deal,
            // but a performance/simplicity tradeoff nonetheless.
            if (await intermediateClauseStore.AddAsync(clause, cancellationToken)) // TODO: with callback for removal - remove all appropriate resolutions from queue.
            {
                // Can resolve with input clauses
                await foreach (var resolution in inputClauseStore.FindResolutions(clause, cancellationToken))
                {
                    queue.Enqueue(resolution);
                }

                // Can also resolve with ancestors of this clause in the proof tree as it stands:
                foreach (var ancestorClause in GetAncestors(clause))
                {
                    foreach (var resolution in ClauseResolution.Resolve(clause, ancestorClause))
                    {
                        queue.Enqueue(resolution);
                    }
                }
            }
        }

        private List<CNFClause> GetAncestors(CNFClause clause)
        {
            var ancestors = new List<CNFClause>();

            // TODO-PERFORMANCE-BREAKING: lots of dictionary lookups.
            // Could be avoided if our proof tree actually had direct references to ancestors.
            while (query.Steps.TryGetValue(clause, out var resolution))
            {
                // NB: only need to look at Clause1 - Clause2 is the side clause, and will
                // be either an input clause (which we look at separately), or an ancestor 
                // of Clause1.
                ancestors.Add(clause = resolution.Clause1);
            }

            return ancestors;
        }

        // side-effect: consumes non-effective from queue. yeah, this is ugly code..
        private bool HasNextEffectiveQueuedResolution()
        {
            do
            {
                if (queue.Count == 0)
                {
                    return false;
                }

                var nextResolution = queue.Peek();

                // non-effective if either of the resolving clauses are no longer in the store (which will be because they are
                // subsumed by another clause since added). Potentially more efficient to remove resolutions from the queue as
                // the subsumed clauses are removed, but this would require more complexity in the queue itself to allow this.
                // For now, this approach will do.
                if (!intermediateClauseStore.ContainsAsync(nextResolution.Clause1).GetAwaiter().GetResult() || !intermediateClauseStore.ContainsAsync(nextResolution.Clause2).GetAwaiter().GetResult())
                {
                    queue.Dequeue(); // nb - no thread safety here, but that's fine for now at least
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
