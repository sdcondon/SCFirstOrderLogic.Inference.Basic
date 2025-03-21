using FluentAssertions;
using FlUnit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static SCFirstOrderLogic.SentenceCreation.OperableSentenceFactory;
using static SCFirstOrderLogic.TestUtilities.GreedyKingsDomain;

namespace SCFirstOrderLogic.Inference.Basic.Fake;

public static class FakeKnowledgeBaseTests
{
    public static Test PositiveScenarios => TestThat
        .GivenTestContext()
        .AndEachOf<TestCase>(() =>
        [
            new(
                Label: "Trivial",
                Query: IsKing(John),
                Knowledge:
                [
                    IsKing(John)
                ])
        ])
        .When((_, tc) =>
        {
            var knowledgeBase = new FakeKnowledgeBase();
            knowledgeBase.Tell(tc.Knowledge);

            var query = knowledgeBase.CreateQuery(tc.Query);
            query.Execute();

            return query;
        })
        .ThenReturns()
        .And((_, _, query) => query.Result.Should().BeTrue());

    public static Test NegativeScenarios => TestThat
        .GivenEachOf<TestCase>(() =>
        [
            new(
                Label: "single conjunct, single step",
                Query: IsEvil(John),
                Knowledge:
                [
                    IsGreedy(John),
                    AllGreedyAreEvil
                ]),

            new(
                Label: "No matching clause",
                Query: IsEvil(X),
                Knowledge:
                [
                    IsKing(John),
                    IsGreedy(John),
                ]),
        ])
        .When(tc =>
        {
            var knowledgeBase = new FakeKnowledgeBase();
            var query = knowledgeBase.CreateQuery(tc.Query);
            query.Execute();

            return query;
        })
        .ThenReturns()
        .And((_, query) => query.Result.Should().BeFalse());

    public static Test RepeatedQueryExecution => TestThat
        .Given(() =>
        {
            var knowledgeBase = new FakeKnowledgeBase();
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

    private record TestCase(string Label, Sentence Query, IEnumerable<Sentence> Knowledge)
    {
        public override string ToString() => Label;
    }
}
