using FluentAssertions;
using FlUnit;
using SCFirstOrderLogic.ClauseIndexing;
using SCFirstOrderLogic.ClauseIndexing.Features;
using SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter8.UsingOperableSentenceFactory;
using SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter9.UsingOperableSentenceFactory;
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
    public static Test BasicPositiveScenarios => TestThat
        .GivenTestContext()
        .AndEachOf(() => new StrategyFactory[]
        {
            new(DelegateStrategyWithHashSetClauseStore),
            new(DelegateStrategyWithFVIClauseStore),
            //new(LinearStrategy_WithFVIClauseStore),
        })
        .AndEachOf(() => new TestCase[]
        {
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
        })
        .WhenAsync(MakeKBAndExecuteQueryAsync)
        .ThenReturns()
        .And((_, _, _, q) => q.Result.Should().BeTrue())
        .And((cxt, _, _, q) => cxt.WriteOutput(q.ResultExplanation));

    public static Test BasicNegativeScenarios => TestThat
        .GivenTestContext()
        .AndEachOf<StrategyFactory>(() =>
        [
            new(DelegateStrategyWithHashSetClauseStore),
            new(DelegateStrategyWithFVIClauseStore),
            //new(LinearStrategy_WithFVIClauseStore),
        ])
        .AndEachOf<TestCase>(() =>
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

    public static Test ComplexPositiveScenarios => TestThat
        .GivenTestContext()
        .AndEachOf<StrategyFactory>(() =>
        [
            new(DelegateStrategyWithHashSetClauseStore),
            new(DelegateStrategyWithFVIClauseStore),
            //new(LinearStrategy_WithFVIClauseStore),
        ])
        .AndEachOf<TestCase>(() =>
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
        .And((_, _, _, q) => q.Result.Should().BeTrue())
        .And((cxt, _, _, q) => cxt.WriteOutput(q.ResultExplanation));

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
                DelegateResolutionStrategy.Filters.None,
                DelegateResolutionStrategy.PriorityComparisons.UnitPreference));

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

    private static IResolutionStrategy DelegateStrategyWithHashSetClauseStore() => new DelegateResolutionStrategy(
        new HashSetClauseStore(),
        DelegateResolutionStrategy.Filters.None,
        DelegateResolutionStrategy.PriorityComparisons.UnitPreference);

    private static IResolutionStrategy DelegateStrategyWithFVIClauseStore() => new DelegateResolutionStrategy(
        new FeatureVectorIndexClauseStore<CloneableAFVIListNode<MaxDepthFeature, CNFClause>, MaxDepthFeature>(
            MaxDepthFeature.MakeFeatureVector,
            new CloneableAFVIListNode<MaxDepthFeature, CNFClause>(MaxDepthFeature.MakeFeatureComparer())),
        DelegateResolutionStrategy.Filters.None,
        DelegateResolutionStrategy.PriorityComparisons.UnitPreference);

    ////private static IResolutionStrategy LinearStrategyWithFVIClauseStore() => new LinearResolutionStrategy(
    ////    new FeatureVectorIndexClauseStore<CloneableAFVIListNode<MaxDepthFeature, CNFClause>, MaxDepthFeature>(
    ////        MaxDepthFeature.MakeFeatureVector,
    ////        new CloneableAFVIListNode<MaxDepthFeature, CNFClause>(MaxDepthFeature.MakeFeatureComparer())));

    private static async Task<ResolutionQuery> MakeKBAndExecuteQueryAsync(ITestContext cxt, StrategyFactory sf, TestCase tc)
    {
        var knowledgeBase = new ResolutionKnowledgeBase(sf.MakeStrategy());
        await knowledgeBase.TellAsync(tc.Knowledge);
        var query = await knowledgeBase.CreateQueryAsync(tc.Query);

        var stepCount = 0;
        while (!query.IsComplete)
        {
            await query.NextStepAsync();
            stepCount++;
        }

        cxt.WriteOutput($"Total steps: {stepCount}");
        return query;
    }

    private record StrategyFactory(
        Func<IResolutionStrategy> MakeStrategy,
        [CallerArgumentExpression(nameof(MakeStrategy))] string? MakeStrategyExpression = null)
    {
        public override string ToString() => MakeStrategyExpression!;
    }

    private record TestCase(
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
