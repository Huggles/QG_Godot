using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>A turn step an expansion adds, run straight after <see cref="After"/> for the faction whose turn it is.</summary>
public sealed record ExtraTurnStep(TurnStep Step, TurnStep After, Func<Faction, Task> Body, TurnStepText Text);

/// <summary>
/// The rules a session adds to the base game, filled in by each enabled expansion's
/// <see cref="IExpansionRules.Configure"/>. Empty for the base game. Core reads it; it never names an expansion.
/// </summary>
public sealed class RuleSet
{
    private readonly List<ExtraTurnStep> _extraTurnSteps = new();

    public IReadOnlyList<ExtraTurnStep> ExtraTurnSteps => _extraTurnSteps;

    /// <summary>How many hand cards Reallocate Resources costs before any card changes it (MutatorReallocateResources.DiscardCostFor).</summary>
    public int ReallocateDiscardCost { get; set; } = 4;

    /// <summary>
    /// The opening deal and the discard that follows it (GameFlow.StartGame, OpeningDiscard). The difference must
    /// stay StaticGameData.HandSize, or turn one starts off the steady-state hand size.
    /// </summary>
    public int OpeningHandSize { get; set; } = StaticGameData.OpeningHandSize;
    public int OpeningDiscardCount { get; set; } = StaticGameData.OpeningDiscardCount;

    /// <summary>The VP a country loses when it discards nothing in its Discard step (DiscardStepHandlerDefault). None in the base game.</summary>
    public int DiscardNothingPenaltyVP { get; set; } = 0;

    /// <summary>The rules of a session played with <paramref name="expansionIds"/>, which must all be loaded.</summary>
    public static RuleSet For(IEnumerable<string> expansionIds)
    {
        RuleSet rules = new();
        foreach (string id in expansionIds)
            foreach (IExpansionRules expansionRules in ExpansionRegistry.Find(id).Rules)
                expansionRules.Configure(rules);
        return rules;
    }

    /// <summary>Adds <paramref name="step"/> to the turn, straight after <paramref name="after"/>, shown on the HUD as <paramref name="text"/>.</summary>
    public void AddTurnStep(TurnStep step, TurnStep after, Func<Faction, Task> body, TurnStepText text)
    {
        if (step <= TurnStep.END || _extraTurnSteps.Any(extra => extra.Step == step))
            throw new InvalidOperationException($"{step} is already a step of the turn");
        _extraTurnSteps.Add(new ExtraTurnStep(step, after, body, text));
    }

    /// <summary>The added step <paramref name="step"/>, or null when it is a base step or not in this session.</summary>
    public ExtraTurnStep FindTurnStep(TurnStep step) => _extraTurnSteps.FirstOrDefault(extra => extra.Step == step);
}
