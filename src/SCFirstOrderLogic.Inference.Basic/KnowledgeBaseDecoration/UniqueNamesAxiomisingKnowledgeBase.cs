// Copyright (c) 2021-2025 Simon Condon.
// You may use this file in accordance with the terms of the MIT license.
using SCFirstOrderLogic.FormulaManipulation;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static SCFirstOrderLogic.FormulaCreation.FormulaFactory;

namespace SCFirstOrderLogic.Inference.Basic.KnowledgeBaseDecoration;

/// <summary>
/// <para>
/// Decorator knowledge base class that adds "axioms" for the unique names assumption as knowledge is added to the underlying knowledge base.
/// </para>
/// <para>
/// Keeps track of all constants that feature in sentences, and adds "not equal" sentences for all pairs
/// with non-equal identifiers. NB: only adds one ordering of arguments, and adds no knowledge that constants
/// are equal to themselves - on the understanding that commutativity/reflexivity will be handled elsewhere
/// (e.g. with <see cref="EqualityAxiomisingKnowledgeBase"/> or with an inner KB that utilises para/demodulation).
/// </para>
/// <para>
/// NB: works only as knowledge is *added* - knowledge already in the inner knowledge base at the time of instantiation
/// will NOT be examined for constants to add unique names knowledge for. This makes this type of no use when using external storage for clauses (since
/// it'll only be able to add non-equality knowledge for identifiers added via the same KB instance).
/// This limitation is ultimately because IKnowledgeBase offers no way to enumerate known facts - and I'm rather reluctant to add this,
/// for several reasons. A decorator clause store for each of the inference algorithms (which absolutely can be enumerated) would be another way 
/// to go - but this has its own problems. Consumers to whom this matters are invited to examine the source code and implement whatever they need based on it.
/// </para>
/// </summary>
// TODO-EXTENSIBILITY: look again at doing this at the clause store level.
public class UniqueNamesAxiomisingKnowledgeBase : IKnowledgeBase
{
    private readonly IKnowledgeBase innerKnowledgeBase;
    private readonly UniqueNamesAxiomiser uniqueNameAxiomiser;

    /// <summary>
    /// Initialises a new instance of the <see cref="UniqueNamesAxiomisingKnowledgeBase"/> class.
    /// </summary>
    /// <param name="innerKnowledgeBase">The inner knowledge base decorated by this class.</param>
    public UniqueNamesAxiomisingKnowledgeBase(IKnowledgeBase innerKnowledgeBase)
    {
        this.innerKnowledgeBase = innerKnowledgeBase;
        uniqueNameAxiomiser = new UniqueNamesAxiomiser(innerKnowledgeBase);
    }

    /// <inheritdoc/>
    public async Task TellAsync(Formula sentence, CancellationToken cancellationToken = default)
    {
        await innerKnowledgeBase.TellAsync(sentence, cancellationToken);
        await uniqueNameAxiomiser.VisitAsync(sentence, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IQuery> CreateQueryAsync(Formula query, CancellationToken cancellationToken = default)
    {
        return innerKnowledgeBase.CreateQueryAsync(query, cancellationToken);
    }

    private class UniqueNamesAxiomiser : RecursiveAsyncFormulaVisitor
    {
        private readonly IKnowledgeBase innerKnowledgeBase;

        // NB #1: the initially empty hash sets here are the real problem being referred to in the 
        // NB in the class summary. This means that, in the external storage case, we'll be 
        // NB #2: We only need to consider the constant (i.e. not the identifier) here
        // because the Function class uses the Identifier for its equality implementation.
        private readonly HashSet<Function> knownConstants = new();

        public UniqueNamesAxiomiser(IKnowledgeBase innerKnowledgeBase)
        {
            this.innerKnowledgeBase = innerKnowledgeBase;
        }

        public override async Task VisitAsync(Function function, CancellationToken cancellationToken = default)
        {
            if (function.Arguments.Count == 0 && !knownConstants.Contains(function))
            {
                foreach (var knownConstant in knownConstants)
                {
                    await innerKnowledgeBase.TellAsync(Not(AreEqual(function, knownConstant)), cancellationToken);
                }

                knownConstants.Add(function);
            }
        }
    }
}
