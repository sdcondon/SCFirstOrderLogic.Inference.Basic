// Copyright (c) 2021-2025 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic.FormulaManipulation;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SCFirstOrderLogic.FormulaCreation.FormulaFactory;

namespace SCFirstOrderLogic.Inference.Basic.KnowledgeBaseDecoration;

/// <summary>
/// <para>
/// Decorator knowledge base class that adds equality axioms as knowledge is added to the underlying knowledge base.
/// </para>
/// <para>
/// NB #1: works only as knowledge is *added* - knowledge already in the inner knowledge base at the time of instantiation
/// will NOT be examined for functions and predicates to add equality rules for. This makes this type poor from a performance perspective when
/// using external storage for clauses (since it'll be consistently trying to add stuff to the clause store that's already there). This limitation
/// is ultimately because IKnowledgeBase offers no way to enumerate known facts - and adding this would be a bad idea. A decorator clause store for
/// each of the inference algorithms (which absolutely can be enumerated) would be another way to go - but this has its own problems. Consumers to
/// whom this matters are invited to examine the source code and implement whatever they need based on it.
/// </para>
/// <para>
/// NB #2: See §9.5.5 ("Equality") of Artifical Intelligence: A Modern Approach for more on dealing with equality by axiomising it.
/// </para>
/// </summary>
// TODO-EXTENSIBILITY: look again at doing this at the clause store level. OR just add injectible dependencies for known identifier repositories.
public class EqualityAxiomisingKnowledgeBase : IKnowledgeBase
{
    private readonly IKnowledgeBase innerKnowledgeBase;
    private readonly PredicateAndFunctionEqualityAxiomiser predicateAndFunctionEqualityAxiomiser;

    /// <summary>
    /// Initialises a new instance of the <see cref="EqualityAxiomisingKnowledgeBase"/> class.
    /// </summary>
    /// <param name="innerKnowledgeBase">The inner knowledge base decorated by this class.</param>
    private EqualityAxiomisingKnowledgeBase(IKnowledgeBase innerKnowledgeBase)
    {
        this.innerKnowledgeBase = innerKnowledgeBase;
        predicateAndFunctionEqualityAxiomiser = new PredicateAndFunctionEqualityAxiomiser(innerKnowledgeBase);
    }

    /// <summary>
    /// <para>
    /// Instantiates and initializes a new instance of the <see cref="EqualityAxiomisingKnowledgeBase"/> class.
    /// </para>
    /// <para>
    /// This method exists and the constructor for <see cref="EqualityAxiomisingKnowledgeBase"/> is private
    /// because complete initialisation here involves telling the knowledge base some things. Telling is asynchronous
    /// because it is potentially long-running (because in "real" clause stores it could easily involve IO), and
    /// including potentially long-running operations in a constructor is generally a bad idea.
    /// </para>
    /// </summary>
    /// <returns>A task that returns a new <see cref="EqualityAxiomisingKnowledgeBase"/> instance.</returns>
    public static async Task<EqualityAxiomisingKnowledgeBase> CreateAsync(IKnowledgeBase innerKnowledgeBase)
    {
        // ..could invoke these in parallel if we wanted to.
        await innerKnowledgeBase.TellAsync(ForAll(X, AreEqual(X, X))); // Reflexivity
        await innerKnowledgeBase.TellAsync(ForAll(X, Y, If(AreEqual(X, Y), AreEqual(Y, X)))); // Commutativity
        await innerKnowledgeBase.TellAsync(ForAll(X, Y, Z, If(And(AreEqual(X, Y), AreEqual(Y, Z)), AreEqual(X, Z)))); // Transitivity

        return new EqualityAxiomisingKnowledgeBase(innerKnowledgeBase);
    }

    /// <inheritdoc/>
    public async Task TellAsync(Formula sentence, CancellationToken cancellationToken = default)
    {
        await innerKnowledgeBase.TellAsync(sentence, cancellationToken);
        await predicateAndFunctionEqualityAxiomiser.VisitAsync(sentence, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IQuery> CreateQueryAsync(Formula query, CancellationToken cancellationToken = default)
    {
        return innerKnowledgeBase.CreateQueryAsync(query, cancellationToken);
    }

    private class PredicateAndFunctionEqualityAxiomiser : RecursiveAsyncFormulaVisitor
    {
        // nb: the initially empty hash sets here are the real problem being referred to in the 
        // nb#1 in the class summary. this means that, in the external storage case, we'll be (trying to) 
        // tell the clause store stuff it already knows..
        private readonly IKnowledgeBase innerKnowledgeBase;
        private readonly HashSet<object> knownPredicateIdentifiers = new() { EqualityIdentifier.Instance };
        private readonly HashSet<object> knownFunctionIdentifiers = new();

        public PredicateAndFunctionEqualityAxiomiser(IKnowledgeBase innerKnowledgeBase)
        {
            this.innerKnowledgeBase = innerKnowledgeBase;
        }

        public override async Task VisitAsync(Predicate predicate, CancellationToken cancellationToken = default)
        {
            // NB: we check only for the identifier, not for the identifier with the particular
            // argument count. A fairly safe assumption that we could nevertheless eliminate at some point.
            if (!knownPredicateIdentifiers.Contains(predicate.Identifier) && predicate.Arguments.Count > 0)
            {
                knownPredicateIdentifiers.Add(predicate.Identifier);

                // For all predicates, we have something like this,
                // depending on the argument count:
                // ∀ l0, r0, l0 = r0 ⇒ [P(l0) ⇔ P(r0)]
                // ∀ l0, r0, l1, r1, [l0 = r0 ∧ l1 = r1] ⇒ [P(l0, l1) ⇔ P(r0, r1)]
                // ... and so on
                var leftArgs = predicate.Arguments.Select((_, i) => new VariableReference($"l{i}")).ToArray();
                var rightArgs = predicate.Arguments.Select((_, i) => new VariableReference($"r{i}")).ToArray();
                var consequent = Iff(new Predicate(predicate.Identifier, leftArgs), new Predicate(predicate.Identifier, rightArgs));

                Formula antecedent = AreEqual(leftArgs[0], rightArgs[0]);
                for (int i = 1; i < predicate.Arguments.Count; i++)
                {
                    antecedent = And(AreEqual(leftArgs[i], rightArgs[i]), antecedent);
                }

                Formula sentence = ForAll(leftArgs[0].Declaration, ForAll(rightArgs[0].Declaration, If(antecedent, consequent)));
                for (int i = 1; i < predicate.Arguments.Count; i++)
                {
                    sentence = ForAll(leftArgs[i].Declaration, ForAll(rightArgs[i].Declaration, sentence));
                }

                await innerKnowledgeBase.TellAsync(sentence, cancellationToken);
            }

            await base.VisitAsync(predicate, cancellationToken);
        }

        public override async Task VisitAsync(Function function, CancellationToken cancellationToken = default)
        {
            // NB: we check only for the identifier, not for the identifier with the particular
            // argument count. A fairly safe assumption that we could nevertheless eliminate at some point.
            if (!knownFunctionIdentifiers.Contains(function.Identifier) && function.Arguments.Count > 0)
            {
                knownFunctionIdentifiers.Add(function.Identifier);

                // For all functions, we have something like this,
                // depending on the argument count:
                // ∀ l0, r0, l0 = r0 ⇒ [F(l0) = F(r0)]
                // ∀ l0, r0, l1, r1, [l0 = r0 ∧ l1 = r1] ⇒ [F(l0, l1) = F(r0, r1)]
                // .. and so on
                var leftArgs = function.Arguments.Select((_, i) => new VariableReference($"l{i}")).ToArray();
                var rightArgs = function.Arguments.Select((_, i) => new VariableReference($"r{i}")).ToArray();
                var consequent = AreEqual(new Function(function.Identifier, leftArgs), new Function(function.Identifier, rightArgs));

                Formula antecedent = AreEqual(leftArgs[0], rightArgs[0]);
                for (int i = 1; i < function.Arguments.Count; i++)
                {
                    antecedent = new Conjunction(AreEqual(leftArgs[i], rightArgs[i]), antecedent);
                }

                Formula sentence = ForAll(leftArgs[0].Declaration, ForAll(rightArgs[0].Declaration, If(antecedent, consequent)));
                for (int i = 1; i < function.Arguments.Count; i++)
                {
                    sentence = ForAll(leftArgs[i].Declaration, ForAll(rightArgs[i].Declaration, sentence));
                }

                await innerKnowledgeBase.TellAsync(sentence, cancellationToken);
            }

            await base.VisitAsync(function, cancellationToken);
        }
    }
}
