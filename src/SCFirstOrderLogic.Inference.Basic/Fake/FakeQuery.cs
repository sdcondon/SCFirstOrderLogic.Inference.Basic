// Copyright (c) 2021-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic.SentenceManipulation.Unification;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Basic.Fake;

/// <summary>
/// An implementation of <see cref="IQuery"/> that uses only the most basic of unification against the directly stored clauses.
/// Used by <see cref="FakeKnowledgeBase"/>.
/// </summary>
public class FakeQuery : IQuery
{
    private readonly CNFSentence queryGoal;
    private readonly List<CNFClause> clauseStore;

    private int executeCount = 0;
    private bool result;

    internal FakeQuery(Sentence queryGoal, List<CNFClause> clauseStore)
    {
        this.queryGoal = queryGoal.ToCNF();
        this.clauseStore = clauseStore;
    }

    /// <inheritdoc />
    public bool IsComplete { get; private set; }

    /// <inheritdoc />
    public bool Result => IsComplete ? result : throw new InvalidOperationException("Query is not yet complete");

    /// <inheritdoc />
    public Task<bool> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // ..while it might be nice to allow for other threads to just get the existing task back
        // if its already been started, the possibility of the cancellation token being different
        // makes it awkward. The complexity added by attempting to deal with that simply isn't worth it.
        // So, we just throw if the query is already in progress. Messing about with a query from
        // multiple threads is fairly unlikely anyway (as opposed wanting an individual query to
        // parallelise itself - which is definitely something I want to look at).
        if (Interlocked.Exchange(ref executeCount, 1) == 1)
        {
            return Task.FromException<bool>(new InvalidOperationException("Query execution has already begun via a prior ExecuteAsync invocation"));
        }

        foreach (var clause in queryGoal.Clauses)
        {
            if (!clause.UnifiesWithAnyOf(clauseStore))
            {
                result = false;
                IsComplete = true;
                return Task.FromResult(Result);
            }
        }

        result = true;
        IsComplete = true;
        return Task.FromResult(Result);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing to do..
        GC.SuppressFinalize(this);
    }
}
