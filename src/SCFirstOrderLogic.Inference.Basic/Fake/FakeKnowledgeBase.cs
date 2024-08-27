// Copyright (c) 2021-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic.SentenceManipulation.Normalisation;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Basic.Fake;

/// <summary>
/// An implementation of <see cref="IKnowledgeBase"/> that just normalises stored knowledge to CNF,
/// and conducts only the most basic of unification on the stored clauses in order to answer queries.
/// Intended for use in tests and demonstrations.
/// </summary>
public class FakeKnowledgeBase : IKnowledgeBase
{
    private readonly List<CNFClause> clauseStore = new();

    /// <inheritdoc />
    public Task TellAsync(Sentence sentence, CancellationToken cancellationToken = default)
    {
        foreach (var clause in sentence.ToCNF().Clauses)
        {
            clauseStore.Add(clause);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    async Task<IQuery> IKnowledgeBase.CreateQueryAsync(Sentence sentence, CancellationToken cancellationToken)
    {
        return await CreateQueryAsync(sentence, cancellationToken);
    }

    /// <summary>
    /// Initiates a new query against the knowledge base.
    /// </summary>
    /// <param name="query">The query sentence.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>A task that returns an <see cref="FakeQuery"/> instance that can be used to execute the query.</returns>
    public Task<FakeQuery> CreateQueryAsync(Sentence query, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FakeQuery(query, clauseStore));
    }

    /// <summary>
    /// Initiates a new query against the knowledge base.
    /// </summary>
    /// <param name="query">The query sentence.</param>
    /// <returns>A <see cref="FakeQuery"/> instance that can be used to execute the query.</returns>
    public FakeQuery CreateQuery(Sentence query)
    {
        return CreateQueryAsync(query).GetAwaiter().GetResult();
    }
}
