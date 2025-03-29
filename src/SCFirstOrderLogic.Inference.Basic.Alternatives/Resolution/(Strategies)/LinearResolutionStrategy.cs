// Copyright (c) 2021-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
// NB: Not quite ready yet. In truth, I'm not completely sure that it's even trying to do the right thing.
// It was created to prove out the strategy abstraction more than anything else.
using System.Runtime.CompilerServices;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// Resolution strategy that applies linear resolution.
/// </summary>
public class LinearResolutionStrategy : IResolutionStrategy
{
    private readonly IKnowledgeBaseClauseStore clauseStore;

    /// <summary>
    /// Initialises a new instance of the <see cref="LinearResolutionStrategy"/> class.
    /// </summary>
    /// <param name="clauseStore">The clause store to use.</param>
    public LinearResolutionStrategy(IKnowledgeBaseClauseStore clauseStore)
    {
        this.clauseStore = clauseStore;
    }

    /// <inheritdoc/>
    public async Task<bool> AddClauseAsync(CNFClause clause, CancellationToken cancellationToken)
    {
        return await clauseStore.AddAsync(clause, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IResolutionQueryStrategy> MakeQueryStrategyAsync(ResolutionQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult((IResolutionQueryStrategy)new QueryStrategy(query, clauseStore, cancellationToken));
    }

    private class QueryStrategy : IResolutionQueryStrategy
    {
        private readonly ResolutionQuery query;

        // jus' a plain old queue for the mo. Not really good enough.
        // prob needs to be prioritised queue which also leverages subsumption.
        private readonly Queue<ClauseResolution> queue = new();
        private readonly Task<IQueryClauseStore> clauseStoreCreation;
        private IQueryClauseStore? clauseStore;

        public QueryStrategy(
            ResolutionQuery query,
            IKnowledgeBaseClauseStore kbClauseStore,
            CancellationToken cancellationToken)
        {
            this.query = query;
            this.clauseStoreCreation = kbClauseStore.CreateQueryStoreAsync(cancellationToken);
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
            clauseStore = await clauseStoreCreation;

            // Initialise the query clause store with the clauses from the negation of the query:
            foreach (var clause in query.NegatedQuerySentence.Clauses)
            {
                await clauseStore.AddAsync(clause, cancellationToken);
            }

            // Queue up initial clause pairings:
            // TODO-PERFORMANCE-MAJOR: potentially repeating a lot of work here - could cache the results of pairings
            // of KB clauses with each other. Or at least don't keep re-attempting ones that we know fail.
            // Is this in scope for this *simple* implementation?
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
            if (query.Steps.TryGetValue(clause, out var resolution))
            {
                yield return resolution.Clause1;

                // TODO: performance - should only need to look at one of the clauses.
                // the side clause will either be an input clause or another ancestor.
                // requires consistent clause 
                foreach (var ancestor in GetAncestors(resolution.Clause1))
                {
                    yield return ancestor;
                }

                // NB: dont need to look at Clause2 - this is the side clause, and will
                // be either an input clause (which we look at separately), or an ancestor 
                // of Clause1.
            }
        }
    }
}
