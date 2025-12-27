// Copyright (c) 2021-2025 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic.ClauseIndexing;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// Implementation of <see cref="IClauseStoreFVINode"/> that just wraps a <see cref="AsyncFeatureVectorIndexListNode{TValue}"/>.
/// </summary>
public class ClauseStoreFVIListNode : IClauseStoreFVINode
{
    private readonly AsyncFeatureVectorIndexListNode<CNFClause> innerNode;

    /// <summary>
    /// Initialises a new instance of the <see cref="ClauseStoreFVIListNode"/> class.
    /// </summary>
    /// <param name="featureComparer"></param>
    public ClauseStoreFVIListNode(IComparer featureComparer)
    {
        innerNode = new(featureComparer);
    }

    /// <inheritdoc/>
    public IComparer FeatureComparer =>
        innerNode.FeatureComparer;

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<FeatureVectorComponent, IAsyncFeatureVectorIndexNode<CNFClause>>> ChildrenAscending =>
        innerNode.ChildrenAscending;

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<FeatureVectorComponent, IAsyncFeatureVectorIndexNode<CNFClause>>> ChildrenDescending =>
        innerNode.ChildrenDescending;

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<CNFClause, CNFClause>> KeyValuePairs =>
        innerNode.KeyValuePairs;

    /// <inheritdoc/>
    public ValueTask AddValueAsync(CNFClause clause, CNFClause value, CancellationToken cancellationToken = default) =>
        innerNode.AddValueAsync(clause, value, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<IAsyncFeatureVectorIndexNode<CNFClause>> GetOrAddChildAsync(FeatureVectorComponent vectorComponent, CancellationToken cancellationToken = default) =>
        innerNode.GetOrAddChildAsync(vectorComponent, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<bool> RemoveValueAsync(CNFClause clause, CancellationToken cancellationToken = default) =>
        innerNode.RemoveValueAsync(clause, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<IAsyncFeatureVectorIndexNode<CNFClause>?> TryGetChildAsync(FeatureVectorComponent vectorComponent, CancellationToken cancellationToken = default) =>
        innerNode.TryGetChildAsync(vectorComponent, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<(bool isSucceeded, CNFClause? value)> TryGetValueAsync(CNFClause clause, CancellationToken cancellationToken = default) =>
        innerNode.TryGetValueAsync(clause, cancellationToken);

    /// <inheritdoc/>
    public ValueTask DeleteChildAsync(FeatureVectorComponent vectorComponent, CancellationToken cancellationToken = default) =>
        innerNode.DeleteChildAsync(vectorComponent, cancellationToken);

    /// <inheritdoc/>
    public Task<IClauseStoreFVINode> CopyAsync()
    {
        var thisCopy = new ClauseStoreFVIListNode(innerNode.FeatureComparer);
        CopyValuesAndChildrenAsync(innerNode, thisCopy.innerNode).GetAwaiter().GetResult();
        return Task.FromResult<IClauseStoreFVINode>(thisCopy);

        static async Task CopyValuesAndChildrenAsync(
            AsyncFeatureVectorIndexListNode<CNFClause> original,
            AsyncFeatureVectorIndexListNode<CNFClause> copy)
        {
            await foreach (var (key, value) in original.KeyValuePairs)
            {
                await copy.AddValueAsync(key, value);
            }

            await foreach (var (featureVectorComponent, child) in original.ChildrenAscending)
            {
                var childCopy = await copy.GetOrAddChildAsync(featureVectorComponent);

                await CopyValuesAndChildrenAsync(
                    (AsyncFeatureVectorIndexListNode<CNFClause>)child,
                    (AsyncFeatureVectorIndexListNode<CNFClause>)childCopy);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // nothing to do - everything's in mem..
    }
}
