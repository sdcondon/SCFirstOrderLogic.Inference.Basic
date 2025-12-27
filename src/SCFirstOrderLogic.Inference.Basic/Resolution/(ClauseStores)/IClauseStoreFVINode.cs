// Copyright (c) 2021-2025 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic.ClauseIndexing;
using System;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// Interface for <see cref="IAsyncFeatureVectorIndexNode{CNFClause}"/> nodes that can also be copied and disposed -
/// and are thus usable by <see cref="FeatureVectorIndexClauseStore"/>.
/// </summary>
public interface IClauseStoreFVINode : IAsyncFeatureVectorIndexNode<CNFClause>, IDisposable
{
    /// <summary>
    /// (Deep) copies the node - used when creating a store for intermediate clauses from a store for the knowledge base as a whole.
    /// </summary>
    /// <returns>A task that gives the new node.</returns>
    Task<IClauseStoreFVINode> CopyAsync();
}
