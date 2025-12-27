using FluentAssertions;
using FlUnit;
using System.Linq;
using static SCFirstOrderLogic.FormulaCreation.Specialised.GenericDomainOperableFormulaFactory;

namespace SCFirstOrderLogic.Inference.Basic.Resolution;

public static partial class ClauseResolutionTests
{
    private record TestCase(CNFClause Clause1, CNFClause Clause2, params CNFClause[] ExpectedResolvents);

    public static Test Resolution => TestThat
        .GivenEachOf<TestCase>(() =>
        [
            // Modus Ponens resolution with a constant
            new(
                Clause1: new(!P(C) | Q(C)), // P(C) => Q(C)
                Clause2: new(P(C)),
                ExpectedResolvents: new CNFClause(Q(C))),

            // Modus Ponens resolution on a globally quantified variable & a constant
            new(
                Clause1: new(!P(X) | Q(X)), // ∀X, P(X) => Q(X)
                Clause2: new(P(C)),
                ExpectedResolvents: new CNFClause(Q(C))), // {X/C}

            // Modus Ponens resolution on a constant & a globally quantified variable
            new(
                Clause1: new(!P(C) | Q(C)), // P(C) => Q(C)
                Clause2: new(P(X)),
                ExpectedResolvents: new CNFClause(Q(C))), // {X/C}

            // Modus Ponens resolution on a globally quantified variable
            new(
                Clause1: new(!P(X) | Q(X)), // ∀X, P(X) => Q(X)
                Clause2: new(P(Y)),
                ExpectedResolvents: new CNFClause(Q(Y))), // {Y/X} .. Or {X/Y}, giving T(X). Should really accept either..

            // More complicated - with a constant
            new(
                Clause1: new(!P(C) | Q(C)), // ¬P(C) ∨ Q(C)
                Clause2: new(P(C) | R(C)), // P(C) ∨ R(C)
                ExpectedResolvents: new CNFClause(Q(C) | R(C))),

            // Complementary unit clauses
            new(
                Clause1: new(P(C)),
                Clause2: new(!P(C)),
                ExpectedResolvents: CNFClause.Empty),

            // Multiply-resolvable clauses
            // There's probably a better (more intuitive) human-language example, here
            new(
                // P(D) ⇒ ¬Q(X). In human, e.g.: "If SnowShoeHater is wearing snowshoes, no-one is wearing a T-shirt"
                Clause1: new(!P(D) | !Q(X)), 
                // ¬Q(C) ⇒ P(Y). In human e.g.: "If TShirtLover is not wearing a T-shirt, everyone is wearing a snowshoes"
                Clause2: new(Q(C) | P(Y)),
                ExpectedResolvents:
                [
                    // {X/C} gives ∀Y, P(Y) ∨ ¬P(D) (that is, P(D) ⇒ P(Y)). If D is P, everything is. (If snowshoehater is wearing snowshoes, everyone is)
                    // NB: becomes obvious by forward chaining Clause1 to Clause2.
                    new(P(Y) | !P(D)), 
                    // {Y/D} gives ∀X, Q(C) ∨ ¬Q(X) (that is, Q(X) ⇒ Q(C)). If anything is Q, C is. (If anyone is wearing a T-shirt, TShirtLover is)
                    // NB: becomes obvious by forward chaining contrapositive of Clause1 to contrapositive of Clause2.
                    new(Q(C) | !Q(X)),
                ]),

            // Variable chain (y=x/x=d) - ordering shouldn't matter
            new(
                Clause1: new(!P(Y, D) | !P(C, Y)), // e.g. ¬Equals(C, y) ∨ ¬Equals(D, y)
                Clause2: new(P(X, X)), // e.g. Equals(x, x)       
                ExpectedResolvents:
                [
                    new(!P(C, D)), // ¬Equals(C, D) 
                    new(!P(C, D)), // ¬Equals(C, D) - don't mind that its returned twice. 
                ]),

            // Variable chain - ordering shouldn't matter
            ////new(
            ////    Clause1: new CNFClause(P(X, X)), // e.g. Equals(x, x)     
            ////    Clause2: new CNFClause(!P(Y, D) | !P(C, Y)), // e.g. ¬Equals(C, y) ∨ ¬Equals(D, y)
            ////    ExpectedResolvents: new[]
            ////    {
            ////        new CNFClause(!P(C, D)), // ¬Equals(C, D) 
            ////        new CNFClause(!P(C, D)), // ¬Equals(C, D) - don't mind that its returned twice. 
            ////    }),

            // Unresolvable - different predicates only
            new(
                Clause1: new CNFClause(P(C)),
                Clause2: new CNFClause(Q(C)),
                ExpectedResolvents: []),

            // Unresolvable - Multiple trivially true resolvents
            new(
                Clause1: new CNFClause(P(C) | !Q(C)),
                Clause2: new CNFClause(!P(C) | Q(C)),
                ExpectedResolvents:
                [
                    // Both of these resolvents are trivially true - we expect them to not be returned
                    ////new CNFClause(P(C) | !P(C)),
                    ////new CNFClause(Q(C) | !Q(C))
                ]),

            // Unresolvable - Multiple trivially true resolvents (with variables..)
            new(
                Clause1: new CNFClause(P(X, Y) | !P(Y, X)),
                Clause2: new CNFClause(P(X, Y) | !P(Y, X)),
                ExpectedResolvents:
                [
                    // Both of these resolvents are trivially true - we expect them to not be returned
                    ////new CNFClause(P(X, X) | !P(X, X)), // with {Y/X}
                    ////new CNFClause(P(Y, Y) | !P(Y, Y)), // with {X/Y}
                ]),
        ])
        .When(g => ClauseResolution.Resolve(g.Clause1, g.Clause2))
        .ThenReturns(((g, r) => r.Select(u => u.Resolvent).Should().BeEquivalentTo(g.ExpectedResolvents)));
}
