using System;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// <para>
/// Useful priority comparison delegates for <see cref="ClauseResolution"/> instances, for use with some resolution strategies.
/// </para>
/// <para>
/// For context, see §9.5.6 ("Resolution Strategies") of 'Artifical Intelligence: A Modern Approach'.
/// </para>
/// </summary>
public static class ClauseResolutionPriorityComparisons
{
    /// <summary>
    /// Naive comparison that orders clause pairs consistently but not according to any particular algorithm.
    /// </summary>
    // NB: While a comparison that just returns zero all the time might seem intuitive here,
    // such a comparison would cause our priority queue to behave more like a stack (see its code - noting
    // that it sensibly doesn't bubble in either direction when the comparison is 0). It'd of course be faster,
    // but I suspect the resulting "depth-first" behaviour of the resolution would be hands-down unhelpful.
    // TODO-ZZ-PERFORMANCE: Better to allow for the strategy to use a regular queue, not a heap-based one - e.g if a ctor overload
    // that simply doesn't have a comparison parameter is invoked. Or of course a different strategy class entirely.
    public static Comparison<ClauseResolution> None { get; } = (x, y) =>
    {
        return x.GetHashCode().CompareTo(y.GetHashCode());
    };

    /// <summary>
    /// Comparison that gives priority to pairs where one of the clauses is a unit clause.
    /// </summary>
    public static Comparison<ClauseResolution> UnitPreference { get; } = (x, y) =>
    {
        var xHasUnitClause = x.Clause1.IsUnitClause || x.Clause2.IsUnitClause;
        var yHasUnitClause = y.Clause1.IsUnitClause || y.Clause2.IsUnitClause;

        if (xHasUnitClause && !yHasUnitClause)
        {
            return 1;
        }
        else if (!xHasUnitClause && yHasUnitClause)
        {
            return -1;
        }
        else
        {
            return 0;
        }
    };

    /// <summary>
    /// <para>
    /// Comparison that gives priority to pairs with a lower total literal count.
    /// </para>
    /// <para>
    /// Not mentioned in source material, but I figured it was a logical variant of unit preference.
    /// </para>
    /// </summary>
    public static Comparison<ClauseResolution> TotalLiteralCountMinimisation { get; } = (x, y) =>
    {
        var xTotalClauseCount = x.Clause1.Literals.Count + x.Clause2.Literals.Count;
        var yTotalClauseCount = y.Clause1.Literals.Count + y.Clause2.Literals.Count;

        if (xTotalClauseCount < yTotalClauseCount)
        {
            return 1;
        }
        else if (xTotalClauseCount > yTotalClauseCount)
        {
            return -1;
        }
        else
        {
            return 0;
        }
    };
}
