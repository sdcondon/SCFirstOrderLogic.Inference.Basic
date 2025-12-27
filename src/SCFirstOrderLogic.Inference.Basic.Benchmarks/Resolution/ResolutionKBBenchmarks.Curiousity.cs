using BenchmarkDotNet.Attributes;
using SCFirstOrderLogic.ClauseIndexing;
using SCFirstOrderLogic.ClauseIndexing.Features;
using SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter9.UsingFormulaFactory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter9.UsingFormulaFactory.CuriousityAndTheCatDomain;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

[MemoryDiagnoser]
[InProcess]
public class ResolutionKBBenchmarks_Curiousity
{
    private static readonly ResolutionKnowledgeBase withFviClauseStore = MakeResolutionKB(MakeFVIClauseStore());
    private static readonly ResolutionKnowledgeBase withFviClauseStoreWrc = MakeResolutionKB_WithRemovalCheck(MakeFVIClauseStore());
    private static readonly ResolutionKnowledgeBase withHSClauseStore = MakeResolutionKB(new HashSetClauseStore());

    [Benchmark]
    public static async Task<bool> WithFeatureVectorIndexClauseStore()
    {
        return await withFviClauseStore.AskAsync(Kills(Curiousity, Tuna));
    }

    [Benchmark]
    public static async Task<bool> WithFeatureVectorIndexClauseStore_WithRemovalCheck()
    {
        return await withFviClauseStoreWrc.AskAsync(Kills(Curiousity, Tuna));
    }

    [Benchmark(Baseline = true)]
    public static async Task<bool> WithHashSetClauseStore()
    {
        return await withHSClauseStore.AskAsync(Kills(Curiousity, Tuna));
    }

    private static ResolutionKnowledgeBase MakeResolutionKB(IKnowledgeBaseClauseStore clauseStore)
    {
        var kb = new ResolutionKnowledgeBase(new DelegateResolutionStrategy(
            clauseStore,
            ClauseResolutionFilters.None,
            ClauseResolutionPriorityComparisons.UnitPreference));

        kb.Tell(CuriousityAndTheCatDomain.Axioms);

        return kb;
    }

    private static ResolutionKnowledgeBase MakeResolutionKB_WithRemovalCheck(IKnowledgeBaseClauseStore clauseStore)
    {
        var kb = new ResolutionKnowledgeBase(new DelegateResolutionStrategy_WithRemovalCheck(
            clauseStore,
            ClauseResolutionFilters.None,
            ClauseResolutionPriorityComparisons.UnitPreference));

        kb.Tell(CuriousityAndTheCatDomain.Axioms);

        return kb;
    }

    private static ResolutionKB_WithoutClauseStore MakeResolutionKB()
    {
        var kb = new ResolutionKB_WithoutClauseStore(
            ResolutionKB_WithoutClauseStore.Filters.None,
            ResolutionKB_WithoutClauseStore.PriorityComparisons.UnitPreference);

        foreach (var axiom in CuriousityAndTheCatDomain.Axioms)
        {
            kb.Tell(axiom);
        }

        return kb;
    }

    private static IKnowledgeBaseClauseStore MakeFVIClauseStore()
    {
        return new FeatureVectorIndexClauseStore(
            MaxDepthFeature.MakeFeatureVector,
            new ClauseStoreFVIListNode(MaxDepthFeature.MakeFeatureComparer()));
    }
}
