// Copyright (c) 2021-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic.SentenceManipulation.VariableManipulation;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Basic.ForwardChaining;

/// <summary>
/// Interface for types that facilitate the storage of CNF definite clauses for a <see cref="ForwardChainingQuery"/>.
/// </summary>
public interface IQueryClauseStore : IAsyncEnumerable<CNFDefiniteClause>, IDisposable
{
    /// <summary>
    /// Stores a clause if it is not already present.
    /// </summary>
    /// <param name="clause">The clause to store.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the clause was added, false if it was already present.</returns>
    Task<bool> AddAsync(CNFDefiniteClause clause, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all of the rules whose conjuncts contain at least one that unifies with at least one of a given set of facts.
    /// </summary>
    /// <param name="facts">The facts.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>An async enumerable of applicable rules.</returns>
    IAsyncEnumerable<CNFDefiniteClause> GetApplicableRules(IEnumerable<Predicate> facts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all known facts that unify with a given fact.
    /// </summary>
    /// <param name="fact">The fact to check for.</param>
    /// <param name="constraints">The variable values that must be respected.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>An async enumerable of fact-unifier pairs - one for each known fact that unifies with the given fact.</returns>
    IAsyncEnumerable<(Predicate knownFact, VariableSubstitution unifier)> MatchWithKnownFacts(Predicate fact, VariableSubstitution constraints, CancellationToken cancellationToken = default);
}
