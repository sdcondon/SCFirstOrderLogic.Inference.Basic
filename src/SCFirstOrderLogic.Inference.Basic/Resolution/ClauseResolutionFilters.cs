// Copyright (c) 2021-2025 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using System;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// <para>
/// Useful filter delegates for <see cref="ClauseResolution"/> instances, for use with some resolution strategies.
/// </para>
/// <para>
/// For context, see §9.5.6 ("Resolution Strategies") of 'Artifical Intelligence: A Modern Approach'.
/// </para>
/// </summary>
public static class ClauseResolutionFilters
{
    /// <summary>
    /// "Filter" that doesn't actually filter out any pairings.
    /// </summary>
    public static Func<ClauseResolution, bool> None { get; } = pair => true;

    /// <summary>
    /// Filter that requires that one of the clauses be a unit clause.
    /// </summary>
    public static Func<ClauseResolution, bool> UnitResolution { get; } = pair => pair.Clause1.IsUnitClause || pair.Clause2.IsUnitClause;
}
