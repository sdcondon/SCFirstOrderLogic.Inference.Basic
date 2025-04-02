using SCFirstOrderLogic.ClauseIndexing;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// Implementation of <see cref="IClauseStoreFVINode{TFeature}"/> that just wraps a <see cref="AsyncFeatureVectorIndexListNode{TFeature, TValue}"/>.
/// </summary>
/// <typeparam name="TFeature">The type of the keys of the feature vectors.</typeparam>
public class ClauseStoreFVIListNode<TFeature> : IClauseStoreFVINode<TFeature>
    where TFeature : notnull
{
    private readonly AsyncFeatureVectorIndexListNode<TFeature, CNFClause> innerNode;

    /// <summary>
    /// Initialises a new instance of the <see cref="ClauseStoreFVIListNode{TFeature}"/> class.
    /// </summary>
    /// <param name="featureComparer"></param>
    public ClauseStoreFVIListNode(IComparer<TFeature> featureComparer)
    {
        innerNode = new(featureComparer);
    }

    /// <inheritdoc/>
    public IComparer<TFeature> FeatureComparer =>
        innerNode.FeatureComparer;

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<FeatureVectorComponent<TFeature>, IAsyncFeatureVectorIndexNode<TFeature, CNFClause>>> ChildrenAscending =>
        innerNode.ChildrenAscending;

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<FeatureVectorComponent<TFeature>, IAsyncFeatureVectorIndexNode<TFeature, CNFClause>>> ChildrenDescending =>
        innerNode.ChildrenDescending;

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<CNFClause, CNFClause>> KeyValuePairs =>
        innerNode.KeyValuePairs;

    /// <inheritdoc/>
    public ValueTask AddValueAsync(CNFClause clause, CNFClause value) =>
        innerNode.AddValueAsync(clause, value);

    /// <inheritdoc/>
    public ValueTask<IAsyncFeatureVectorIndexNode<TFeature, CNFClause>> GetOrAddChildAsync(FeatureVectorComponent<TFeature> vectorComponent) =>
        innerNode.GetOrAddChildAsync(vectorComponent);

    /// <inheritdoc/>
    public ValueTask<bool> RemoveValueAsync(CNFClause clause) =>
        innerNode.RemoveValueAsync(clause);

    /// <inheritdoc/>
    public ValueTask<IAsyncFeatureVectorIndexNode<TFeature, CNFClause>?> TryGetChildAsync(FeatureVectorComponent<TFeature> vectorComponent) =>
        innerNode.TryGetChildAsync(vectorComponent);

    /// <inheritdoc/>
    public ValueTask<(bool isSucceeded, CNFClause? value)> TryGetValueAsync(CNFClause clause) =>
        innerNode.TryGetValueAsync(clause);

    /// <inheritdoc/>
    public ValueTask DeleteChildAsync(FeatureVectorComponent<TFeature> vectorComponent) =>
        innerNode.DeleteChildAsync(vectorComponent);

    /// <inheritdoc/>
    public Task<IClauseStoreFVINode<TFeature>> CopyAsync()
    {
        var thisCopy = new ClauseStoreFVIListNode<TFeature>(innerNode.FeatureComparer);
        CopyValuesAndChildrenAsync(innerNode, thisCopy.innerNode).GetAwaiter().GetResult();
        return Task.FromResult<IClauseStoreFVINode<TFeature>>(thisCopy);

        static async Task CopyValuesAndChildrenAsync(
            AsyncFeatureVectorIndexListNode<TFeature, CNFClause> original,
            AsyncFeatureVectorIndexListNode<TFeature, CNFClause> copy)
        {
            await foreach (var (key, value) in original.KeyValuePairs)
            {
                await copy.AddValueAsync(key, value);
            }

            await foreach (var (featureVectorComponent, child) in original.ChildrenAscending)
            {
                var childCopy = await copy.GetOrAddChildAsync(featureVectorComponent);

                await CopyValuesAndChildrenAsync(
                    (AsyncFeatureVectorIndexListNode<TFeature, CNFClause>)child,
                    (AsyncFeatureVectorIndexListNode<TFeature, CNFClause>)childCopy);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // nothing to do - everything's in mem..
    }
}
