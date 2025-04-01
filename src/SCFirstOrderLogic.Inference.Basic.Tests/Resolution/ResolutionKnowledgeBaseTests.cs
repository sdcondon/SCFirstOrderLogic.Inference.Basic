using FluentAssertions;
using FlUnit;
using SCFirstOrderLogic.ClauseIndexing;
using SCFirstOrderLogic.ClauseIndexing.Features;
using SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter8.UsingOperableSentenceFactory;
using SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter9.UsingOperableSentenceFactory;
using SCFirstOrderLogic.Inference.Basic.KnowledgeBaseDecoration;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter8.UsingOperableSentenceFactory.KinshipDomain;
using static SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter9.UsingOperableSentenceFactory.CrimeDomain;
using static SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter9.UsingOperableSentenceFactory.CuriousityAndTheCatDomain;
using static SCFirstOrderLogic.SentenceCreation.OperableSentenceFactory;
using static SCFirstOrderLogic.TestUtilities.GreedyKingsDomain;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

public static class ResolutionKnowledgeBaseTests
{
    public static Test TrivialPositiveScenarios => TestThat
        .GivenTestContext()
        .AndEachOf<KBFactory>(() =>
        [
            new(UnitPrefWithHSClauseStore),
            new(UnitPrefWithFVIClauseStore),
            new(LinearWithHSClauseStore),
            new(LinearWithFVIClauseStore),
            new(LinearWithoutIntClauseStorage),
        ])
        .AndEachOf<KnowledgeAndQuery>(() =>
        [
            new( // Trivial
                Query: IsKing(John),
                Knowledge:
                [
                    IsKing(John)
                ]),

            new( // Single conjunct, single step
                Query: IsEvil(John),
                Knowledge:
                [
                    IsGreedy(John),
                    AllGreedyAreEvil
                ]),

            new( // Two conjuncts, single step
                Query: IsEvil(John),
                Knowledge:
                [
                    IsGreedy(John),
                    IsKing(John),
                    AllGreedyKingsAreEvil
                ]),

            new( // Two applicable rules, each with two conjuncts, single step
                Query: ThereExists(X, IsEvil(X)),
                Knowledge:
                [
                    IsKing(John),
                    IsGreedy(Mary),
                    IsQueen(Mary),
                    AllGreedyKingsAreEvil,
                    AllGreedyQueensAreEvil,
                ]),

            new( // Multiple possible substitutions
                Query: ThereExists(X, IsKing(X)),
                Knowledge:
                [
                    IsKing(John),
                    IsKing(Richard),
                ]),

            new( // Uses same var twice in same proof
                Query: Knows(John, Mary),
                Knowledge:
                [
                    AllGreedyAreEvil,
                    AllEvilKnowEachOther,
                    IsGreedy(John),
                    IsGreedy(Mary),
                ]),
        ])
        .WhenAsync(MakeKBAndExecuteQueryAsync)
        .ThenReturns()
        .And((_, _, _, q) => q.Result.Should().BeTrue());

    public static Test TrivialNegativeScenarios => TestThat
        .GivenTestContext()
        .AndEachOf<KBFactory>(() =>
        [
            new(UnitPrefWithHSClauseStore),
            new(UnitPrefWithFVIClauseStore),
            new(LinearWithHSClauseStore),
            new(LinearWithFVIClauseStore),
            new(LinearWithoutIntClauseStorage),
        ])
        .AndEachOf<KnowledgeAndQuery>(() =>
        [
            new( // No matching clause
                Query: IsEvil(John),
                Knowledge:
                [
                    IsKing(John),
                    IsGreedy(John),
                ]),

            new( // Clause with not all conjuncts satisfied
                Query: IsEvil(John),
                Knowledge:
                [
                    IsKing(John),
                    AllGreedyKingsAreEvil,
                ]),

            new( // No unifier will work - x is either John or Richard - it can't be both:
                Query: ThereExists(X, IsEvil(X)),
                Knowledge:
                [
                    IsKing(John),
                    IsGreedy(Richard),
                    AllGreedyKingsAreEvil,
                ]),
        ])
        .WhenAsync(MakeKBAndExecuteQueryAsync)
        .ThenReturns()
        .And((_, _, _, q) => q.Result.Should().BeFalse());

    public static Test SimplePositiveScenarios => TestThat
        .GivenTestContext()
        .AndEachOf<KBFactory>(() =>
        [
            new(UnitPrefWithHSClauseStore),
            new(UnitPrefWithFVIClauseStore),
            new(LinearWithHSClauseStore),
            new(LinearWithFVIClauseStore),
            new(LinearWithoutIntClauseStorage),
        ])
        .AndEachOf<KnowledgeAndQuery>(() =>
        [
            new(
                Query: IsCriminal(ColonelWest),
                Knowledge: CrimeDomain.Axioms),

            new(
                Query: Kills(Curiousity, Tuna),
                Knowledge: CuriousityAndTheCatDomain.Axioms),
        ])
        .WhenAsync(MakeKBAndExecuteQueryAsync)
        .ThenReturns()
        .And((_, _, _, q) => q.Result.Should().BeTrue());

    // This is a difficult query. Would need more complex algo to deal with it
    // in a timely fashion. Better way of handling equality, better prioritisation, etc.
    ////public static Test KinshipExample => TestThat
    ////    .GivenTestContext()
    ////    .WhenAsync(async _ =>
    ////    {
    ////        var resolutionStrategy = DelegateResolutionStrategy_WithFVIClauseStore();
    ////        var innerKb = new ResolutionKnowledgeBase(resolutionStrategy);
    ////        var kb = await EqualityAxiomisingKnowledgeBase.CreateAsync(innerKb);
    ////        await kb.TellAsync(KinshipDomain.Axioms);
    ////        var query = await kb.CreateQueryAsync(ForAll(X, Y, Iff(IsSibling(X, Y), IsSibling(Y, X))));
    ////        await query.ExecuteAsync();
    ////        return query;
    ////    })
    ////    .ThenReturns()
    ////    .And((_, retVal) => retVal.Result.Should().Be(true))
    ////    .And((ctx, retVal) => ctx.WriteOutputLine(((ResolutionQuery)retVal).ResultExplanation));

    public static Test RepeatedQueryExecution => TestThat
        .Given(() =>
        {
            var knowledgeBase = new ResolutionKnowledgeBase(new DelegateResolutionStrategy(
                new HashSetClauseStore(),
                ClauseResolutionFilters.None,
                ClauseResolutionPriorityComparisons.UnitPreference));

            return knowledgeBase.CreateQuery(IsGreedy(John));
        })
        .WhenAsync(async q =>
        {
            var task1 = q.ExecuteAsync();
            var task2 = q.ExecuteAsync();

            try
            {
                await Task.WhenAll(task1, task2);
            }
            catch (InvalidOperationException) { }

            return (task1, task2);
        })
        .ThenReturns((q, rv) =>
        {
            (rv.task1.IsFaulted ^ rv.task2.IsFaulted).Should().BeTrue();
        });

    private static ResolutionKnowledgeBase UnitPrefWithHSClauseStore()
    {
        return new ResolutionKnowledgeBase(new DelegateResolutionStrategy(
            new HashSetClauseStore(),
            ClauseResolutionFilters.None,
            ClauseResolutionPriorityComparisons.UnitPreference));
    }

    private static ResolutionKnowledgeBase UnitPrefWithFVIClauseStore()
    {
        var clauseStore = new FeatureVectorIndexClauseStore<CloneableAFVIListNode<MaxDepthFeature, CNFClause>, MaxDepthFeature>(
            MaxDepthFeature.MakeFeatureVector,
            new CloneableAFVIListNode<MaxDepthFeature, CNFClause>(MaxDepthFeature.MakeFeatureComparer()));

        return new ResolutionKnowledgeBase(new DelegateResolutionStrategy(
            clauseStore,
            ClauseResolutionFilters.None,
            ClauseResolutionPriorityComparisons.UnitPreference));
    }

    private static ResolutionKnowledgeBase LinearWithHSClauseStore()
    {
        return new ResolutionKnowledgeBase(new LinearResolutionStrategy(
            new HashSetClauseStore(),
            ClauseResolutionPriorityComparisons.UnitPreference));
    }

    private static ResolutionKnowledgeBase LinearWithFVIClauseStore()
    {
        var clauseStore = new FeatureVectorIndexClauseStore<CloneableAFVIListNode<MaxDepthFeature, CNFClause>, MaxDepthFeature>(
            MaxDepthFeature.MakeFeatureVector,
            new CloneableAFVIListNode<MaxDepthFeature, CNFClause>(MaxDepthFeature.MakeFeatureComparer()));

        return new ResolutionKnowledgeBase(new LinearResolutionStrategy(
            clauseStore,
            ClauseResolutionPriorityComparisons.UnitPreference));
    }

    private static ResolutionKnowledgeBase LinearWithoutIntClauseStorage()
    {
        var clauseStore = new FeatureVectorIndexClauseStore<CloneableAFVIListNode<MaxDepthFeature, CNFClause>, MaxDepthFeature>(
            MaxDepthFeature.MakeFeatureVector,
            new CloneableAFVIListNode<MaxDepthFeature, CNFClause>(MaxDepthFeature.MakeFeatureComparer()));

        return new ResolutionKnowledgeBase(new LinearResolutionStrategy_WithoutIntermediateClauseStorage(
            clauseStore,
            ClauseResolutionPriorityComparisons.UnitPreference));
    }

    ////private static async Task<IKnowledgeBase> UnitPrefAndEqualityAxiomsWithFVIClauseStore()
    ////{
    ////    var clauseStore = new FeatureVectorIndexClauseStore<CloneableAFVIListNode<MaxDepthFeature, CNFClause>, MaxDepthFeature>(
    ////        MaxDepthFeature.MakeFeatureVector,
    ////        new CloneableAFVIListNode<MaxDepthFeature, CNFClause>(MaxDepthFeature.MakeFeatureComparer()));

    ////    var innerKb = new ResolutionKnowledgeBase(new DelegateResolutionStrategy(
    ////        clauseStore,
    ////        ClauseResolutionFilters.None,
    ////        ClauseResolutionPriorityComparisons.UnitPreference));

    ////    return await EqualityAxiomisingKnowledgeBase.CreateAsync(innerKb);
    ////}

    private static async Task<IQuery> MakeKBAndExecuteQueryAsync(ITestContext cxt, KBFactory kbf, KnowledgeAndQuery tc)
    {
        var knowledgeBase = kbf.MakeKB();
        await knowledgeBase.TellAsync(tc.Knowledge);
        var query = await knowledgeBase.CreateQueryAsync(tc.Query);

        if (query is ResolutionQuery resolutionQuery)
        {
            var stepCount = 0;
            while (!resolutionQuery.IsComplete)
            {
                await resolutionQuery.NextStepAsync();
                stepCount++;
            }

            cxt.WriteOutput($"Total steps: {stepCount}");

            if (resolutionQuery.Result)
            {
                cxt.WriteOutput(Environment.NewLine);
                cxt.WriteOutput(resolutionQuery.ResultExplanation);
            }
        }
        else
        {
            query.Execute();
        }

        return query;
    }

    private record KBFactory(
        Func<IKnowledgeBase> MakeKB,
        [CallerArgumentExpression(nameof(MakeKB))] string? MakeKBExpression = null)
    {
        public override string ToString() => MakeKBExpression!;
    }

    private record KnowledgeAndQuery(
        Sentence Query,
        IEnumerable<Sentence> Knowledge,
        [CallerArgumentExpression(nameof(Knowledge))] string? KnowledgeExpression = null)
    {
        public override string ToString() => $"given {KnowledgeExpression}, {Query}";
    }

    private class CloneableAFVIListNode<TFeature, TValue>(IComparer<TFeature> featureComparer) : IAsyncFeatureVectorIndexNode<TFeature, TValue>, ICloneable, IDisposable
        where TFeature : notnull
    {
        private readonly AsyncFeatureVectorIndexListNode<TFeature, TValue> innerNode = new(featureComparer);

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
            var thisCopy = new CloneableAFVIListNode<TFeature, TValue>(innerNode.FeatureComparer);
            CopyValuesAndChildrenAsync(innerNode, thisCopy.innerNode).GetAwaiter().GetResult();
            return thisCopy;

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
