// Copyright (c) 2021-2025 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic.SentenceManipulation.Normalisation;
using SCFirstOrderLogic.SentenceManipulation.VariableManipulation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Basic.BackwardChaining;

/// <summary>
/// Implementation of <see cref="IClauseStore"/> that just uses an in-memory dictionary (keyed by consequent identifier) to store known clauses.
/// </summary>
public class DictionaryClauseStore : IClauseStore
{
    // NB: the inner dictionary here is intended more as a hash set - but system.collections.concurrent doesn't
    // offer a concurrent hash set (and I want strong concurrency support). Not worth adding a third-party package for
    // this though. Consumers to whom this matters will likely be considering creating their own clause store anyway.
    private readonly ConcurrentDictionary<object, ConcurrentDictionary<CNFDefiniteClause, byte>> clausesByConsequentPredicateId = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryClauseStore"/> class.
    /// </summary>
    public DictionaryClauseStore() { }

    /// <summary>
    /// <para>
    /// Initializes a new instance of the <see cref="DictionaryClauseStore"/> class that is pre-populated with some knowledge.
    /// </para>
    /// <para>
    /// NB: Of course, most implementations of <see cref="IClauseStore"/> won't have a constructor for pre-population, because most
    /// clause stores will do IO when adding knowledge, and including long-running operations in a ctor is generally a bad idea.
    /// We only include it here because of (the in-memory nature of this implementation and) its usefulness for tests.
    /// </para>
    /// </summary>
    /// <param name="sentences">The initial content of the store.</param>
    public DictionaryClauseStore(IEnumerable<Sentence> sentences)
    {
        foreach (var sentence in sentences)
        {
            foreach (var clause in sentence.ToCNF().Clauses)
            {
                if (!clause.IsDefiniteClause)
                {
                    throw new ArgumentException($"All forward chaining knowledge must be expressable as definite clauses. The normalisation of {sentence} includes {clause}, which is not a definite clause");
                }

                AddAsync(new CNFDefiniteClause(clause)).GetAwaiter().GetResult();
            }
        }
    }

    /// <inheritdoc/>
    public Task<bool> AddAsync(CNFDefiniteClause clause, CancellationToken cancellationToken = default)
    {
        if (!clausesByConsequentPredicateId.TryGetValue(clause.Consequent.Identifier, out var clausesWithThisConsequentPredicateId))
        {
            clausesWithThisConsequentPredicateId = clausesByConsequentPredicateId[clause.Consequent.Identifier] = new ConcurrentDictionary<CNFDefiniteClause, byte>();
        }

        return Task.FromResult(clausesWithThisConsequentPredicateId.TryAdd(clause, 0));
    }

    /// <inheritdoc />
    public async IAsyncEnumerator<CNFDefiniteClause> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        // Ensure we complete asynchronously. Clause stores are the only async thing in queries, so if we don't
        // yield back to the context anywhere in here, we run the risk of entire queries executing synchronously,
        // which we probably never want given the potential for queries to go on for a long time. Yes, adds overhead,
        // and this clause store implementation is only really intended as an example (see class summary), but we
        // should probably at least try to behave "nicely".
        await Task.Yield();

        foreach (var clauseList in clausesByConsequentPredicateId.Values)
        {
            foreach (var clause in clauseList.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return clause;
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<(CNFDefiniteClause Clause, VariableSubstitution Substitution)> GetClauseApplications(
        Predicate goal,
        VariableSubstitution constraints,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (clausesByConsequentPredicateId.TryGetValue(goal.Identifier, out var clausesWithThisGoal))
        {
            // Ensure we complete asynchronously. Clause stores are the only async thing in queries, so if we don't
            // yield back to the context anywhere in here, we run the risk of entire queries executing synchronously,
            // which we probably never want given the potential for queries to go on for a long time. Yes, adds overhead,
            // and this clause store implementation is only really intended as an example (see class summary), but we
            // should probably at least try to behave "nicely".
            await Task.Yield();

            foreach (var clause in clausesWithThisGoal.Keys)
            {
                // TODO-CODE-STINK: restandardisation doesn't belong here - the need to restandardise is due to the algorithm we use.
                // A query other than SimpleBackwardChain might not need this (if e.g. it had a different unifier instance for each step).
                // TODO*-BUG?: hmm, looks odd. we restandardise, THEN do a thing involving the constraint.. When could the constraint ever
                // kick in? Verify test coverage here..
                var restandardisedClause = clause.Restandardise();

                if (Unifier.TryUpdate(restandardisedClause.Consequent, goal, constraints, out var substitution))
                {
                    yield return (restandardisedClause, substitution);
                }
            }
        }
    }
}
