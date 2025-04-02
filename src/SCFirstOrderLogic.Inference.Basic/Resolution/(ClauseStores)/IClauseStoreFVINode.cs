using SCFirstOrderLogic.ClauseIndexing;
using System;
using System.Threading.Tasks;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

/// <summary>
/// Interface for <see cref="IAsyncFeatureVectorIndexNode{TFeature, CNFClause}"/> nodes that can also be copied and disposed -
/// and are thus usable by <see cref="FeatureVectorIndexClauseStore{TFeature}"/>.
/// </summary>
/// <typeparam name="TFeature"></typeparam>
public interface IClauseStoreFVINode<TFeature> : IAsyncFeatureVectorIndexNode<TFeature, CNFClause>, IDisposable
{
    /// <summary>
    /// (Deep) copies the node - used when creating a store for intermediate clauses from a store for the knowledge base as a whole.
    /// </summary>
    /// <returns>A task that gives the new node.</returns>
    Task<IClauseStoreFVINode<TFeature>> CopyAsync();
}
