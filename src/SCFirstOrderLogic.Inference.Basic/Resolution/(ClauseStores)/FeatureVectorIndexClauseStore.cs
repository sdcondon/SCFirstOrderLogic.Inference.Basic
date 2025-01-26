// Copyright (c) 2021-2024 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic.ClauseIndexing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// An implementation of <see cref="IKnowledgeBaseClauseStore"/> that maintains all known clauses in
/// a feature vector index.
/// </summary>
public class FeatureVectorIndexClauseStore<TNode, TFeature> : IKnowledgeBaseClauseStore
    where TNode : IAsyncFeatureVectorIndexNode<TFeature, CNFClause>, ICloneable, IDisposable
    where TFeature : notnull
{
    private readonly Func<CNFClause, IEnumerable<FeatureVectorComponent<TFeature>>> featureVectorMaker;
    private readonly TNode featureVectorIndexRoot;
    private readonly AsyncFeatureVectorIndex<TFeature> featureVectorIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureVectorIndexClauseStore{TNode, TFeature}"/> class.
    /// </summary>
    /// <param name="featureVectorMaker">Delegate that makes the feature vector for a given clause.</param>
    /// <param name="featureVectorIndexRoot">The root node to use for the feature vector index.</param>
    public FeatureVectorIndexClauseStore(
        Func<CNFClause, IEnumerable<FeatureVectorComponent<TFeature>>> featureVectorMaker,
        TNode featureVectorIndexRoot)
    {
        this.featureVectorMaker = featureVectorMaker;
        this.featureVectorIndexRoot = featureVectorIndexRoot;

        featureVectorIndex = new AsyncFeatureVectorIndex<TFeature>(
            featureVectorMaker,
            featureVectorIndexRoot);
    }

    /// <inheritdoc />
    public async Task<bool> AddAsync(CNFClause clause, CancellationToken cancellationToken = default)
    {
        // todo: at the mo, the fvi is providing us no real benefit, because we'll have already queued up
        // the resolutions for the subsumed clauses. what needs to happen here is passing in a callback
        // that ultimately ensures we dont attempt to resolve any subsumed clauses. needs a change in design.
        return await featureVectorIndex.TryReplaceSubsumedAsync(clause);
    }

    /// <inheritdoc />
    public IAsyncEnumerator<CNFClause> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return featureVectorIndex.GetAsyncEnumerator(cancellationToken);
    }

    /// <inheritdoc />
    public Task<IQueryClauseStore> CreateQueryStoreAsync(CancellationToken cancellationToken = default)
    {
        // TODO-BREAKING: icloneable -> something async? might need our own interface..
        return Task.FromResult<IQueryClauseStore>(new QueryStore(
            featureVectorMaker,
            (TNode)featureVectorIndexRoot.Clone()));
    }

    /// <summary>
    /// Implementation of <see cref="IQueryClauseStore"/> that is used solely by <see cref="HashSetClauseStore"/>.
    /// </summary>
    private class QueryStore : IQueryClauseStore
    {
        private readonly TNode featureVectorIndexRoot;
        private readonly AsyncFeatureVectorIndex<TFeature> featureVectorIndex;

        public QueryStore(
            Func<CNFClause, IEnumerable<FeatureVectorComponent<TFeature>>> featureVectorSelector,
            TNode featureVectorIndexRoot)
        {
            this.featureVectorIndexRoot = featureVectorIndexRoot;

            featureVectorIndex = new AsyncFeatureVectorIndex<TFeature>(
                featureVectorSelector,
                featureVectorIndexRoot);
        }

        /// <inheritdoc />
        public async Task<bool> AddAsync(CNFClause clause, CancellationToken cancellationToken = default)
        {
            return await featureVectorIndex.TryReplaceSubsumedAsync(clause);
        }

        /// <inheritdoc />
        public IAsyncEnumerator<CNFClause> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return featureVectorIndex.GetAsyncEnumerator(cancellationToken);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<ClauseResolution> FindResolutions(
            CNFClause clause,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var otherClause in this.WithCancellation(cancellationToken))
            {
                foreach (var resolution in ClauseResolution.Resolve(clause, otherClause))
                {
                    yield return resolution;
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            featureVectorIndexRoot.Dispose();
        }
    }
}
