// Copyright (c) 2021-2025 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
// NB: Not quite ready yet. In truth, I'm not completely sure that it's even trying to do the right thing.
// It was created to prove out the strategy abstraction more than anything else.
using SCFirstOrderLogic.Inference.Basic.InternalUtilities;
using System.Runtime.CompilerServices;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// Resolution strategy that applies linear resolution.
/// </summary>
public class LinearResolutionStrategy_WithoutIntermediateClauseStorage : IResolutionStrategy
{
    private readonly IKnowledgeBaseClauseStore clauseStore;
    private readonly Comparison<ClauseResolution> priorityComparison;

    /// <summary>
    /// Initialises a new instance of the <see cref="LinearResolutionStrategy_WithoutIntermediateClauseStorage"/> class.
    /// </summary>
    /// <param name="clauseStore">The clause store to use.</param>
    public LinearResolutionStrategy_WithoutIntermediateClauseStorage(
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
        return new QueryStrategy(query, await clauseStore.CreateQueryStoreAsync(cancellationToken), priorityComparison);
    }

    private class QueryStrategy : IResolutionQueryStrategy
    {
        private readonly ResolutionQuery query;
        private readonly IQueryClauseStore clauseStore;
        private readonly MaxPriorityQueue<ClauseResolution> queue;

        public QueryStrategy(
            ResolutionQuery query,
            IQueryClauseStore clauseStore,
            Comparison<ClauseResolution> priorityComparison)
        {
            this.query = query;
            this.clauseStore = clauseStore;
            this.queue = new MaxPriorityQueue<ClauseResolution>(priorityComparison);
        }

        /// <inheritdoc />
        public bool IsQueueEmpty => queue.Count == 0;

        /// <inheritdoc />
        public ClauseResolution DequeueResolution() => queue.Dequeue();

        /// <inheritdoc />
        public void Dispose()
        {
            clauseStore?.Dispose();
        }

        /// <inheritdoc />
        public async Task EnqueueInitialResolutionsAsync(CancellationToken cancellationToken)
        {
            // Initialise the query clause store with the clauses from the negation of the query:
            foreach (var clause in query.NegatedQuerySentence.Clauses)
            {
                await clauseStore.AddAsync(clause, cancellationToken);
            }

            // Queue up initial clause pairings:
            // TODO-PERFORMANCE-MAJOR: potentially repeating a lot of work here - could cache the results of pairings
            // of KB clauses with each other. Or at least don't keep re-attempting ones that we know fail.
            // Is this in scope for this *basic* implementation?
            await foreach (var clause in clauseStore)
            {
                await foreach (var resolution in clauseStore.FindResolutions(clause, cancellationToken))
                {
                    queue.Enqueue(resolution);
                }
            }
        }

        /// <inheritdoc />
        public async Task EnqueueResolutionsAsync(CNFClause clause, CancellationToken cancellationToken)
        {
            await foreach (var newResolution in FindResolutions(clause, cancellationToken))
            {
                queue.Enqueue(newResolution);
            }
        }

        private async IAsyncEnumerable<ClauseResolution> FindResolutions(
            CNFClause clause,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Can resolve with clauses from the KB and negated query - which we use the inner clause store for
            await foreach (var resolution in clauseStore!.FindResolutions(clause, cancellationToken))
            {
                yield return resolution;
            }

            // Can also resolve with ancestors of this clause in the proof tree as it stands:
            foreach (var ancestorClause in GetAncestors(clause))
            {
                foreach (var resolution in ClauseResolution.Resolve(clause, ancestorClause))
                {
                    yield return resolution;
                }
            }
        }

        private IEnumerable<CNFClause> GetAncestors(CNFClause clause)
        {
            // TODO-PERFORMANCE-BREAKING: lots of dictionary lookups.
            // Could be avoided if our proof tree actually had direct references to ancestors.
            while (query.Steps.TryGetValue(clause, out var resolution))
            {
                // NB: dont need to look at Clause2 - this is the side clause, and will
                // be either an input clause (which we look at separately), or an ancestor 
                // of Clause1.
                yield return clause = resolution.Clause1;
            }
        }
    }
}
