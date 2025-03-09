// Copyright (c) 2021-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// Interface for types that facilitate the storage of CNF clauses for a <see cref="ResolutionQuery"/>.
/// </summary>
public interface IQueryClauseStore : IAsyncEnumerable<CNFClause>, IDisposable
{
    /// <summary>
    /// Stores a clause if it is not already present.
    /// </summary>
    /// <param name="clause">The clause to store.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the clause was added, false if it was already present.</returns>
    Task<bool> AddAsync(CNFClause clause, CancellationToken cancellationToken = default);

    Task<bool> ContainsAsync(CNFClause clause, CancellationToken cancellationToken = default);

    /// <summary>
    /// <para>
    /// Returns all possible resolutions of a given clause with some clause in the store.
    /// </para>
    /// <para>
    /// NB: store implementations might limit what resolutions are "possible", depending
    /// on the resolution strategy they implement.
    /// </para>
    /// </summary>
    /// <param name="clause">The clause to find resolutions for.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An enumerable of resolutions of the given clause with a clause in the store.</returns>
    IAsyncEnumerable<ClauseResolution> FindResolutions(CNFClause clause, CancellationToken cancellationToken = default);
}
