using System.Collections.Generic;
using System.Linq;

/// <summary>
/// When the faction must give up hand cards (Reallocate's discards, a discard cost, the opening discard,
/// Rosie the Riveter), give up the ones worth least to keep (<see cref="DelayedCardValue.KeepValue"/>); when
/// it reorders its deck (Superior Planning), put the best on top. An optional give-up (Rosie) only takes
/// cards worth less than an average card (<see cref="ProjectionValuer.CardWorth"/>), and declines when there are none.
/// </summary>
public sealed class GiveUpWorstCardsRule : IBotRule
{
    public string Name => "give_up_worst_cards";

    public string Description => "Discard or put back the hand cards worth least to keep; reorder the deck best first";

    public double DefaultWeight => 1.0;

    public bool EnabledByDefault => false;

    public IReadOnlySet<string> Kinds { get; } = new HashSet<string>
        { "ForceDiscardHandCards", "SelectCards", "ReorderCards" };

    public IReadOnlySet<CliOptionKind> OptionKinds { get; } = new HashSet<CliOptionKind> { CliOptionKind.Card };

    /// <summary>Only the faction's own cards: a multi-select over anything else is not a give-up.</summary>
    public bool AppliesTo(BotDecision decision)
    {
        if (GameFlow.Instance == null) return false;
        FactionRecord own = BoardState.Live.ForFaction(decision.Faction);
        HashSet<int> pile = decision.Spec.Kind == "ReorderCards" ? own.Deck.ToHashSet() : own.Hand.ToHashSet();
        return decision.Options.All(o => o.Kind == CliOptionKind.Card && pile.Contains(o.Id));
    }

    public void Apply(BotDecision decision, IBotVerdictSink sink)
    {
        bool reorder = decision.Spec.Kind == "ReorderCards";
        bool optional = decision.Spec.MinSelections == 0 && decision.Spec.Kind == "SelectCards";
        double draw = optional ? ProjectionValuer.CardWorth(decision.Faction) : 0;

        for (int i = 0; i < decision.Options.Count; i++)
        {
            double keep = DelayedCardValue.KeepValue(decision.Options[i].Id, decision.Faction);
            if (optional && keep >= draw) sink.Veto(i, $"worth {keep:0.##}, an average draw {draw:0.##}");
            else sink.Score(i, reorder ? keep : -keep);
        }
    }
}
