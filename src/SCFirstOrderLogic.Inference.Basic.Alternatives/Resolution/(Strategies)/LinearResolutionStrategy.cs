﻿// Copyright (c) 2021-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
// NB: Not quite ready yet. In truth, I'm not completely sure that it's even trying to do the right thing.
// It was created to prove out the strategy abstraction more than anything else.
using SCFirstOrderLogic.Inference.Basic.InternalUtilities;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// Resolution strategy that applies linear resolution.
/// </summary>
public class LinearResolutionStrategy : IResolutionStrategy
{
    private readonly IKnowledgeBaseClauseStore clauseStore;
    private readonly Comparison<ClauseResolution> priorityComparison;

    /// <summary>
    /// Initialises a new instance of the <see cref="LinearResolutionStrategy"/> class.
    /// </summary>
    /// <param name="clauseStore">The clause store to use.</param>
    public LinearResolutionStrategy(
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
            // Check if we've found a new clause (i.e. something that we didn't know already).
            // Downside of using Add: clause store will encounter itself when looking for unifiers - not a big deal,
            // but a performance/simplicity tradeoff nonetheless.
            if (await clauseStore.AddAsync(clause, cancellationToken)) // TODO: with callback for removal - remove all appropriate resolutions from queue.
            {
                var ancestors = GetAncestors(clause);

                await foreach (var resolution in clauseStore.FindResolutions(clause, cancellationToken))
                {
                    // Can resolve with input clauses (which won't have an entry in steps dictionary),
                    // or with an ancestor. Performance could almost certainly be improved by specialised
                    // clause store with more indexing, but can come back to that later perhaps.
                    if (!query.Steps.ContainsKey(resolution.Clause2) || ancestors.Contains(resolution.Clause2))
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
    }
}
