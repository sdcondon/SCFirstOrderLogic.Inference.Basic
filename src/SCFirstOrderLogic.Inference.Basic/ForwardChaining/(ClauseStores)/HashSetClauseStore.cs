﻿// Copyright (c) 2021-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic.SentenceManipulation.Normalisation;
using SCFirstOrderLogic.SentenceManipulation.VariableManipulation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Basic.ForwardChaining;

/// <summary>
/// A (rather inefficient) implementation of <see cref="IClauseStore"/> that just uses a hash set to store clauses
/// (and performs no indexing).
/// </summary>
public class HashSetClauseStore : IKnowledgeBaseClauseStore
{
    // Yes, not actually a hash set, despite the name of the class. I want strong concurrency support,
    // but System.Collections.Concurrent doesn't include a hash set implementation. Using a ConcurrentDictionary
    // means some overhead (the values), but its not worth doing anything else for this basic implementation.
    // Might consider using a third-party package for an actual concurrent hash set at some point, but.. probably won't.
    // Consumers to whom this matters can always create their own implementation.
    private readonly ConcurrentDictionary<CNFDefiniteClause, byte> clauses = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HashSetClauseStore"/> class.
    /// </summary>
    public HashSetClauseStore() { }

    /// <summary>
    /// <para>
    /// Initializes a new instance of the <see cref="HashSetClauseStore"/> class that is pre-populated with some knowledge.
    /// </para>
    /// <para>
    /// NB: Of course, most implementations of <see cref="IClauseStore"/> won't have a constructor for pre-population, because most
    /// clause stores will do IO when adding knowledge, and including long-running operations in a ctor is generally a bad idea.
    /// We only include it here because of (the in-memory nature of this implementation and) its usefulness for tests.
    /// </para>
    /// </summary>
    /// <param name="sentences">The initial content of the store.</param>
    public HashSetClauseStore(IEnumerable<Sentence> sentences)
    {
        foreach (var sentence in sentences)
        {
            foreach (var clause in sentence.ToCNF().Clauses)
            {
                if (!clause.IsDefiniteClause)
                {
                    throw new ArgumentException($"All forward chaining knowledge must be expressable as definite clauses. The normalisation of {sentence} includes {clause}, which is not a definite clause");
                }

                clauses.TryAdd(new CNFDefiniteClause(clause), 0);
            }
        }
    }

    /// <inheritdoc/>
    public Task<bool> AddAsync(CNFDefiniteClause clause, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(clauses.TryAdd(clause, 0));
    }

#pragma warning disable CS1998 // async lacks await.. Could add await Task.Yield() to silence this, but it is not worth the overhead.
    /// <inheritdoc />
    public async IAsyncEnumerator<CNFDefiniteClause> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        foreach (var clause in clauses.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return clause;
        }
    }
#pragma warning restore CS1998

    /// <inheritdoc />
    public Task<IQueryClauseStore> CreateQueryStoreAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IQueryClauseStore>(new QueryStore(clauses));
    }

    /// <summary>
    /// Implementation of <see cref="IQueryClauseStore"/> that is used solely by <see cref="HashSetClauseStore"/>.
    /// </summary>
    private class QueryStore : IQueryClauseStore
    {
        private readonly ConcurrentDictionary<CNFDefiniteClause, byte> clauses;

        public QueryStore(IEnumerable<KeyValuePair<CNFDefiniteClause, byte>> clauses) => this.clauses = new(clauses);

        /// <inheritdoc />
        public Task<bool> AddAsync(CNFDefiniteClause clause, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(clauses.TryAdd(clause, 0));
        }

#pragma warning disable CS1998 // async lacks await.. Could add await Task.Yield() to silence this, but it is not worth the overhead.
        /// <inheritdoc />
        public async IAsyncEnumerator<CNFDefiniteClause> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var clause in clauses.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return clause;
            }
        }
#pragma warning restore CS1998

        /// <inheritdoc />
        public async IAsyncEnumerable<CNFDefiniteClause> GetApplicableRules(
            IEnumerable<Predicate> facts,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            bool AnyConjunctUnifiesWithAnyFact(CNFDefiniteClause clause)
            {
                foreach (var conjunct in clause.Conjuncts)
                {
                    foreach (var fact in facts)
                    {
                        if (Unifier.TryCreate(conjunct, fact, out _))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            await foreach (var clause in this.WithCancellation(cancellationToken))
            {
                if (AnyConjunctUnifiesWithAnyFact(clause))
                {
                    yield return clause;
                }
            }
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<(Predicate knownFact, VariableSubstitution unifier)> MatchWithKnownFacts(
            Predicate fact,
            VariableSubstitution constraints,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Here we just iterate through ALL known predicates trying to find something that unifies with the fact.
            // A better implementation would do some kind of indexing (or at least store facts and rules separately):
            await foreach (var knownClause in this.WithCancellation(cancellationToken))
            {
                if (knownClause.IsUnitClause)
                {
                    if (Unifier.TryUpdate(knownClause.Consequent, fact, constraints, out var unifier))
                    {
                        yield return (knownClause.Consequent, unifier);
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            //// Nothing to do..
        }
    }
}
