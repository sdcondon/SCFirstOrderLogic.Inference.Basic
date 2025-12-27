using FluentAssertions;
using FlUnit;
using SCFirstOrderLogic.TestUtilities;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using static SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter8.UsingFormulaFactory.KinshipDomain;
using static SCFirstOrderLogic.FormulaCreation.FormulaFactory;

namespace SCFirstOrderLogic.Inference.Basic.KnowledgeBaseDecoration;

public static class UniqueNamesAxiomisingKnowledgeBaseTests
{
    public static Test Smoke => TestThat
        .Given(() => new MockKnowledgeBase())
        .WhenAsync(async kb =>
        {
            var sut = new UniqueNamesAxiomisingKnowledgeBase(kb);
            await sut.TellAsync(IsMale(new Function("Bob")));
            await sut.TellAsync(IsMale(new Function("Larry")));
            await sut.TellAsync(Not(IsMale(new Function("Alex"))));
        })
        .ThenReturns()
        .And(kb =>
        {
            kb.Sentences.Should().BeEquivalentTo(
                expectation:
                [
                    IsMale(new Function("Bob")), // Sentence that we told it
                    IsMale(new Function("Larry")), // Sentence that we told it
                    Not(IsMale(new Function("Alex"))), // Sentence that we told it
                    Not(AreEqual(new Function("Larry"), new Function("Bob"))),
                    Not(AreEqual(new Function("Alex"), new Function("Bob"))),
                    Not(AreEqual(new Function("Alex"), new Function("Larry"))),
                ],
                config: EquivalencyOptions.UsingOnlyConsistencyForVariables);
        });

    private class MockKnowledgeBase : IKnowledgeBase
    {
        public Collection<Formula> Sentences { get; } = [];

        public Task TellAsync(Formula sentence, CancellationToken cancellationToken = default)
        {
            Sentences.Add(sentence);
            return Task.CompletedTask;
        }

        public Task<IQuery> CreateQueryAsync(Formula query, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
