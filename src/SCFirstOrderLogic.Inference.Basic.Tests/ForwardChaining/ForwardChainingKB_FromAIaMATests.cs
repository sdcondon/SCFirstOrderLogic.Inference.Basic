﻿using FluentAssertions;
using FlUnit;
using SCFirstOrderLogic.Inference;
using System.Collections.Generic;
using static SCFirstOrderLogic.ExampleDomains.FromAIaMA.Chapter9.UsingSentenceFactory.CrimeDomain;
using static SCFirstOrderLogic.SentenceCreation.OperableSentenceFactory;
using static SCFirstOrderLogic.TestUtilities.GreedyKingsDomain;

namespace SCFirstOrderLogic.Inference.Basic.ForwardChaining;

public static class ForwardChainingKB_FromAIaMATests
{
    public static Test PositiveScenarios => TestThat
        .GivenTestContext()
        .AndEachOf<ForwardChainingKB_FromAIaMA.Query>(() =>
        [
            // Trivial
            // Commented out because it actually fails given the book listing.. Don't want to deviate from the reference implementation though,
            // so just commenting out the test. See SimpleForwardChainingKnowledgeBase for the fix..
            ////MakeQuery(
            ////    query: IsKing(John),
            ////    kb: new Sentence[]
            ////    {
            ////        IsKing(John)
            ////    }),
            
            // Trivial - with multiple substitutions
            // Commented out because it actually fails given the book listing.. Don't want to deviate from the reference implementation though,
            // so just commenting out the test. See SimpleForwardChainingKnowledgeBase for the fix..
            ////MakeQuery(
            ////    query: IsKing(X),
            ////    kb: new Sentence[]
            ////    {
            ////        IsKing(John),
            ////        IsKing(Richard),
            ////    }),

            // single conjunct, single step
            MakeQuery(
                query: IsEvil(John),
                kb:
                [
                    IsGreedy(John),
                    AllGreedyAreEvil
                ]),

            // two conjuncts, single step
            MakeQuery(
                query: IsEvil(John),
                kb:
                [
                    IsGreedy(John),
                    IsKing(John),
                    AllGreedyKingsAreEvil
                ]),

            // two conjuncts, single step, with red herrings
            MakeQuery(
                query: IsEvil(X),
                kb:
                [
                    IsKing(John),
                    IsGreedy(Mary),
                    IsQueen(Mary),
                    AllGreedyKingsAreEvil,
                    AllGreedyQueensAreEvil,
                ]),

            // Uses same var twice in same proof
            MakeQuery(
                query: Knows(John, Mary),
                kb:
                [
                    AllGreedyAreEvil,
                    AllEvilKnowEachOther,
                    IsGreedy(John),
                    IsGreedy(Mary),
                ]),

            // More complex - Crime example domain
            MakeQuery(
                query: IsCriminal(ColonelWest),
                kb: Axioms),
        ])
        .When((cxt, query) => query.Execute())
        .ThenReturns()
        .And((_, _, rv) => rv.Should().BeTrue())
        .And((_, query, _) => query.Result.Should().BeTrue());

    public static Test NegativeScenarios => TestThat
        .GivenEachOf<ForwardChainingKB_FromAIaMA.Query>(() =>
        [
            // no matching clause
            MakeQuery(
                query: IsEvil(X),
                kb:
                [
                    IsKing(John),
                    IsGreedy(John),
                ]),

            // clause with not all conjuncts satisfied
            MakeQuery(
                query: IsEvil(X),
                kb:
                [
                    IsKing(John),
                    AllGreedyKingsAreEvil,
                ]),

            // no unifier will work - x is either John or Richard - it can't be both:
            MakeQuery(
                query: IsEvil(X),
                kb:
                [
                    IsKing(John),
                    IsGreedy(Richard),
                    AllGreedyKingsAreEvil,
                ]),
        ])
        .When(query => query.Execute())
        .ThenReturns()
        .And((_, rv) => rv.Should().BeFalse())
        .And((query, _) => query.Result.Should().BeFalse());

    private static ForwardChainingKB_FromAIaMA.Query MakeQuery(Sentence query, IEnumerable<Sentence> kb)
    {
        var knowledgeBase = new ForwardChainingKB_FromAIaMA();
        knowledgeBase.Tell(kb);
        return (ForwardChainingKB_FromAIaMA.Query)knowledgeBase.CreateQuery(query);
    }
}
