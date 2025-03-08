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
    public static Test PositiveScenarios => TestThat
        .GivenTestContext()
        .AndEachOf(() => new StrategyFactory[]
        {
            new(DelegateResolutionStrategy_WithHashSetClauseStore),
            new(DelegateResolutionStrategy_WithFVIClauseStore),
            //new(NewLinearResolutionStrategy),
        })
        .AndEachOf(() => new TestCase[]
        {
            new( // trivial
                query: IsKing(John),
                knowledge: new Sentence[]
                {
                    IsKing(John)
                }),

            new( // single conjunct, single step
                query: IsEvil(John),
                knowledge: new Sentence[]
                {
                    IsGreedy(John),
                    AllGreedyAreEvil
                }),

            new( // Two conjuncts, single step
                query: IsEvil(John),
                knowledge: new Sentence[]
                {
                    IsGreedy(John),
                    IsKing(John),
                    AllGreedyKingsAreEvil
                }),

            new( // Two applicable rules, each with two conjuncts, single step
                query: ThereExists(X, IsEvil(X)),
                knowledge: new Sentence[]
                {
                    IsKing(John),
                    IsGreedy(Mary),
                    IsQueen(Mary),
                    AllGreedyKingsAreEvil,
                    AllGreedyQueensAreEvil,
                }),

            new( // Multiple possible substitutions
                query: ThereExists(X, IsKing(X)),
                knowledge: new Sentence[]
                {
                    IsKing(John),
                    IsKing(Richard),
                }),

            new( // Uses same var twice in same proof
                query: Knows(John, Mary),
                knowledge: new Sentence[]
                {
                    AllGreedyAreEvil,
                    AllEvilKnowEachOther,
                    IsGreedy(John),
                    IsGreedy(Mary),
                }),

            new( // More complex - Crime example domain
                query: IsCriminal(ColonelWest),
                knowledge: CrimeDomain.Axioms),

            new( // More complex with some non-definite clauses - curiousity and the cat example domain
                query: Kills(Curiousity, Tuna),
                knowledge: CuriousityAndTheCatDomain.Axioms),
        })
        .WhenAsync(async (cxt, sf, tc) =>
        {
            var knowledgeBase = new ResolutionKnowledgeBase(sf.makeStrategy());
            await knowledgeBase.TellAsync(tc.knowledge);
            var query = await knowledgeBase.CreateQueryAsync(tc.query);

            var stepCount = 0;
            while (!query.IsComplete)
            {
                await query.NextStepAsync();
                stepCount++;
            }

            cxt.WriteOutput($"Total steps: {stepCount}");
            return query;
        })
        .ThenReturns()
        .And((_, _, _, q) => q.Result.Should().BeTrue())
        .And((cxt, _, _, q) => cxt.WriteOutput(q.ResultExplanation));

    public static Test NegativeScenarios => TestThat
        .GivenEachOf(() => new StrategyFactory[]
        {
            new(DelegateResolutionStrategy_WithHashSetClauseStore),
            new(DelegateResolutionStrategy_WithFVIClauseStore),
            //new(NewLinearResolutionStrategy),
        })
        .AndEachOf(() => new TestCase[]
        {
            new( // no matching clause
                query: IsEvil(John),
                knowledge: new Sentence[]
                {
                    IsKing(John),
                    IsGreedy(John),
                }),

            new( // clause with not all conjuncts satisfied
                query: IsEvil(John),
                knowledge: new Sentence[]
                {
                    IsKing(John),
                    AllGreedyKingsAreEvil,
                }),

            new( // no unifier will work - x is either John or Richard - it can't be both:
                query: ThereExists(X, IsEvil(X)),
                knowledge: new Sentence[]
                {
                    IsKing(John),
                    IsGreedy(Richard),
                    AllGreedyKingsAreEvil,
                }),
        })
        .When((sf, tc) =>
        {
            var knowledgeBase = new ResolutionKnowledgeBase(sf.makeStrategy());
            knowledgeBase.Tell(tc.knowledge);
            var query = knowledgeBase.CreateQuery(tc.query);
            query.Execute();
            return query;
        })
        .ThenReturns()
        .And((_, _, q) => q.Result.Should().BeFalse());

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

    // This is a difficult query. Would need more complex algo to deal with it
    // in a timely fashion. Better way of handling equality, better prioritisation, etc.
    ////public static Test KinshipExample => TestThat
    ////    .GivenTestContext()
    ////    .WhenAsync(async _ =>
    ////    {
    ////        var resolutionStrategy = new LinearResolutionStrategy(new HashSetClauseStore());
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

    private static IResolutionStrategy DelegateResolutionStrategy_WithHashSetClauseStore() => new DelegateResolutionStrategy(
        new HashSetClauseStore(),
        DelegateResolutionStrategy.Filters.None,
        DelegateResolutionStrategy.PriorityComparisons.UnitPreference);

    // todo: can't use parameterless MakeFeatureComparer - needs to be able to deal with skolem fn and standardised variable ids
    // (and in general the equality identifier isn't a string either). prob need to provide some facility to help deal with this.
    private static IResolutionStrategy DelegateResolutionStrategy_WithFVIClauseStore() => new DelegateResolutionStrategy(
        new FeatureVectorIndexClauseStore<CloneableAFVIListNode<MaxDepthFeature, CNFClause>, MaxDepthFeature>(
            MaxDepthFeature.MakeFeatureVector,
            new CloneableAFVIListNode<MaxDepthFeature, CNFClause>(MaxDepthFeature.MakeFeatureComparer())),
        DelegateResolutionStrategy.Filters.None,
        DelegateResolutionStrategy.PriorityComparisons.UnitPreference);

    //// private static IResolutionStrategy NewLinearResolutionStrategy() => new LinearResolutionStrategy(new HashSetClauseStore());

    private record StrategyFactory(
        Func<IResolutionStrategy> makeStrategy,
        [CallerArgumentExpression("makeStrategy")] string? makeStrategyExpression = null)
    {
        public override string ToString() => makeStrategyExpression!;
    }

    private record TestCase(
        Sentence query,
        IEnumerable<Sentence> knowledge,
        [CallerArgumentExpression("knowledge")] string? knowledgeExpression = null)
    {
        public override string ToString() => $"{query}; given knowledge: {knowledgeExpression}";
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
