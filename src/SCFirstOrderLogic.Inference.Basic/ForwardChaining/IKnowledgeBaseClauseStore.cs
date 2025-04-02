// Copyright (c) 2021-2025 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Basic.ForwardChaining;

/// <summary>
/// Interface for types that facilitate the storage of CNF definite clauses for a <see cref="ForwardChainingKnowledgeBase"/>.
/// In particular, defines methods for storing a clause, and for creating a <see cref="IQueryClauseStore"/> for storing the intermediate clauses of an individual query.
/// </summary>
public interface IKnowledgeBaseClauseStore : IAsyncEnumerable<CNFDefiniteClause>
{
    /// <summary>
    /// Stores a clause if it is not already present.
    /// </summary>
    /// <param name="clause">The clause to store.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the clause was added, false if it was already present.</returns>
    Task<bool> AddAsync(CNFDefiniteClause clause, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a (disposable) copy of the current store, for storing the intermediate clauses of a particular query.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>A task, the result of which is a new <see cref="IQueryClauseStore"/> instance.</returns>
    Task<IQueryClauseStore> CreateQueryStoreAsync(CancellationToken cancellationToken = default);
}
