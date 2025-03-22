using BenchmarkDotNet.Attributes;
using SCFirstOrderLogic.ClauseIndexing;
using SCFirstOrderLogic.ClauseIndexing.Features;
using SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter9.UsingSentenceFactory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter9.UsingSentenceFactory.CrimeDomain;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

// TODO: not 100% fair - should factor KB initialisation out of benchmark, really.
[MemoryDiagnoser]
[InProcess]
public class ResolutionKBBenchmarks_Crime
{
    private static readonly ResolutionKnowledgeBase withFviClauseStore = MakeResolutionKB(MakeFVIClauseStore());
    private static readonly ResolutionKnowledgeBase withFviClauseStoreWrc = MakeResolutionKB_WithRemovalCheck(MakeFVIClauseStore());
    private static readonly ResolutionKnowledgeBase withHSClauseStore = MakeResolutionKB(new HashSetClauseStore());
    private static readonly ResolutionKB_WithoutClauseStore withoutClauseStore = MakeResolutionKB();

    [Benchmark]
    public static async Task<bool> WithFVIClauseStore()
    {
        return await withFviClauseStore.AskAsync(IsCriminal(ColonelWest));
    }

    [Benchmark]
    public static async Task<bool> WithFVIClauseStore_WithRemoveCheck()
    {
        return await withFviClauseStoreWrc.AskAsync(IsCriminal(ColonelWest));
    }

    [Benchmark(Baseline = true)]
    public static async Task<bool> WithHSClauseStore()
    {
        return await withHSClauseStore.AskAsync(IsCriminal(ColonelWest));
    }

    [Benchmark]
    public static bool WithoutClauseStore()
    {
        return withoutClauseStore.Ask(IsCriminal(ColonelWest));
    }

    private static ResolutionKnowledgeBase MakeResolutionKB(IKnowledgeBaseClauseStore clauseStore)
    {
        var kb = new ResolutionKnowledgeBase(new DelegateResolutionStrategy(
            clauseStore,
            DelegateResolutionStrategy.Filters.None,
            DelegateResolutionStrategy.PriorityComparisons.UnitPreference));

        kb.Tell(CrimeDomain.Axioms);

        return kb;
    }

    private static ResolutionKnowledgeBase MakeResolutionKB_WithRemovalCheck(IKnowledgeBaseClauseStore clauseStore)
    {
        var kb = new ResolutionKnowledgeBase(new DelegateResolutionStrategy_WithRemovalCheck(
            clauseStore,
            DelegateResolutionStrategy.Filters.None,
            DelegateResolutionStrategy.PriorityComparisons.UnitPreference));

        kb.Tell(CrimeDomain.Axioms);

        return kb;
    }

    private static ResolutionKB_WithoutClauseStore MakeResolutionKB()
    {
        var kb = new ResolutionKB_WithoutClauseStore(
            ResolutionKB_WithoutClauseStore.Filters.None,
            ResolutionKB_WithoutClauseStore.PriorityComparisons.UnitPreference);

        foreach (var axiom in CrimeDomain.Axioms)
        {
            kb.Tell(axiom);
        }

        return kb;
    }

    private static IKnowledgeBaseClauseStore MakeFVIClauseStore()
    {
        return new FeatureVectorIndexClauseStore<CloneableAFVIListNode<MaxDepthFeature, CNFClause>, MaxDepthFeature>(
            MaxDepthFeature.MakeFeatureVector,
            new CloneableAFVIListNode<MaxDepthFeature, CNFClause>(MaxDepthFeature.MakeFeatureComparer()));
    }

    private class CloneableAFVIListNode<TFeature, TValue> : IAsyncFeatureVectorIndexNode<TFeature, TValue>, ICloneable, IDisposable
        where TFeature : notnull
    {
        private readonly AsyncFeatureVectorIndexListNode<TFeature, TValue> innerNode;

        public CloneableAFVIListNode(IComparer<TFeature> featureComparer) =>
            innerNode = new AsyncFeatureVectorIndexListNode<TFeature, TValue>(featureComparer);

        public IComparer<TFeature> FeatureComparer =>
            innerNode.FeatureComparer;

        public IAsyncEnumerable<KeyValuePair<FeatureVectorComponent<TFeature>, IAsyncFeatureVectorIndexNode<TFeature, TValue>>> ChildrenAscending =>
            innerNode.ChildrenAscending;

        public IAsyncEnumerable<KeyValuePair<FeatureVectorComponent<TFeature>, IAsyncFeatureVectorIndexNode<TFeature, TValue>>> ChildrenDescending =>
            innerNode.ChildrenDescending;

        public IAsyncEnumerable<KeyValuePair<CNFClause, TValue>> KeyValuePairs =>
            innerNode.KeyValuePairs;

        public ValueTask AddValueAsync(CNFClause clause, TValue value) =>
            innerNode.AddValueAsync(clause, value);

        public ValueTask<IAsyncFeatureVectorIndexNode<TFeature, TValue>> GetOrAddChildAsync(FeatureVectorComponent<TFeature> vectorComponent) => 
            innerNode.GetOrAddChildAsync(vectorComponent);

        public ValueTask<bool> RemoveValueAsync(CNFClause clause) =>
            innerNode.RemoveValueAsync(clause);

        public ValueTask<IAsyncFeatureVectorIndexNode<TFeature, TValue>?> TryGetChildAsync(FeatureVectorComponent<TFeature> vectorComponent) => 
            innerNode.TryGetChildAsync(vectorComponent);

        public ValueTask<(bool isSucceeded, TValue? value)> TryGetValueAsync(CNFClause clause) =>
            innerNode.TryGetValueAsync(clause);

        public ValueTask DeleteChildAsync(FeatureVectorComponent<TFeature> vectorComponent) =>
            innerNode.DeleteChildAsync(vectorComponent);

        public object Clone()
        {
            var copy = new CloneableAFVIListNode<TFeature, TValue>(innerNode.FeatureComparer);
            CopyValuesAndChildrenAsync(innerNode, copy.innerNode).GetAwaiter().GetResult();
            return copy;

            static async Task CopyValuesAndChildrenAsync(
                AsyncFeatureVectorIndexListNode<TFeature, TValue> original,
                AsyncFeatureVectorIndexListNode<TFeature, TValue> copy)
            {
                await foreach (var (key, value) in original.KeyValuePairs)
                {
                    await copy.AddValueAsync(key, value);
                }

                await foreach (var (featureVectorComponent, child) in original.ChildrenAscending)
                {
                   var childCopy = await copy.GetOrAddChildAsync(featureVectorComponent);

                   await CopyValuesAndChildrenAsync(
                       (AsyncFeatureVectorIndexListNode<TFeature, TValue>)child,
                       (AsyncFeatureVectorIndexListNode<TFeature, TValue>)childCopy);
                }
            }
        }

        public void Dispose()
        {
            // nothing to do - everything's in mem..
        }
    }
}
