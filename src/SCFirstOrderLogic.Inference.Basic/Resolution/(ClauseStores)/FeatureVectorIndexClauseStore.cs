// Copyright (c) 2021-2025 Simon Condon.
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
/// a <see cref="AsyncFeatureVectorIndex"/>.
/// </summary>
public class FeatureVectorIndexClauseStore : IKnowledgeBaseClauseStore
{
    private readonly Func<CNFClause, IEnumerable<FeatureVectorComponent>> featureVectorMaker;
    private readonly IClauseStoreFVINode featureVectorIndexRoot;
    private readonly AsyncFeatureVectorIndex featureVectorIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureVectorIndexClauseStore"/> class.
    /// </summary>
    /// <param name="featureVectorMaker">Delegate that makes the feature vector for a given clause.</param>
    /// <param name="featureVectorIndexRoot">The root node to use for the feature vector index.</param>
    public FeatureVectorIndexClauseStore(
        Func<CNFClause, IEnumerable<FeatureVectorComponent>> featureVectorMaker,
        IClauseStoreFVINode featureVectorIndexRoot)
    {
        this.featureVectorMaker = featureVectorMaker;
        this.featureVectorIndexRoot = featureVectorIndexRoot;

        featureVectorIndex = new AsyncFeatureVectorIndex(
            featureVectorMaker,
            featureVectorIndexRoot);
    }

    /// <inheritdoc />
    public async Task<bool> AddAsync(CNFClause clause, CancellationToken cancellationToken = default)
    {
        return await featureVectorIndex.TryReplaceSubsumedAsync(clause, null, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerator<CNFClause> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return featureVectorIndex.GetAsyncEnumerator(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IQueryClauseStore> CreateQueryStoreAsync(CancellationToken cancellationToken = default)
    {
        return new QueryStore(
            featureVectorMaker,
            await featureVectorIndexRoot.CopyAsync());
    }

    /// <summary>
    /// Implementation of <see cref="IQueryClauseStore"/> that is used solely by <see cref="HashSetClauseStore"/>.
    /// </summary>
    private class QueryStore : IQueryClauseStore
    {
        private readonly IClauseStoreFVINode featureVectorIndexRoot;
        private readonly AsyncFeatureVectorIndex featureVectorIndex;

        public QueryStore(
            Func<CNFClause, IEnumerable<FeatureVectorComponent>> featureVectorSelector,
            IClauseStoreFVINode featureVectorIndexRoot)
        {
            this.featureVectorIndexRoot = featureVectorIndexRoot;

            featureVectorIndex = new AsyncFeatureVectorIndex(
                featureVectorSelector,
                featureVectorIndexRoot);
        }

        /// <inheritdoc />
        public async Task<bool> AddAsync(CNFClause clause, CancellationToken cancellationToken = default)
        {
            return await featureVectorIndex.TryReplaceSubsumedAsync(clause, null, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> AddAsync(CNFClause clause, Func<CNFClause, Task> removedClauseCallback, CancellationToken cancellationToken = default)
        {
            return await featureVectorIndex.TryReplaceSubsumedAsync(clause, removedClauseCallback, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> ContainsAsync(CNFClause clause, CancellationToken cancellationToken = default)
        {
            return await featureVectorIndex.ContainsAsync(clause, cancellationToken);
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
            // todo: feels like we should be able to filter somehow just using the FVI - what's the relationship
            // (if any) between *resolution* potential and feature vectors? think about/read up.
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
