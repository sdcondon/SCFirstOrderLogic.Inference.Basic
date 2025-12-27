using BenchmarkDotNet.Attributes;
using SCFirstOrderLogic.ClauseIndexing;
using SCFirstOrderLogic.ClauseIndexing.Features;
using SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter9.UsingFormulaFactory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter9.UsingFormulaFactory.CrimeDomain;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

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
            ClauseResolutionFilters.None,
            ClauseResolutionPriorityComparisons.UnitPreference));

        kb.Tell(CrimeDomain.Axioms);

        return kb;
    }

    private static ResolutionKnowledgeBase MakeResolutionKB_WithRemovalCheck(IKnowledgeBaseClauseStore clauseStore)
    {
        var kb = new ResolutionKnowledgeBase(new DelegateResolutionStrategy_WithRemovalCheck(
            clauseStore,
            ClauseResolutionFilters.None,
            ClauseResolutionPriorityComparisons.UnitPreference));

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
        return new FeatureVectorIndexClauseStore(
            MaxDepthFeature.MakeFeatureVector,
            new ClauseStoreFVIListNode(MaxDepthFeature.MakeFeatureComparer()));
    }
}
