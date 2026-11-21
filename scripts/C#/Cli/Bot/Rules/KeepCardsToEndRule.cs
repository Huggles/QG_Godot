using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Veto a play or activation that uses up extra cards (Reallocate, discard costs) when what is left in hand
/// and deck would no longer cover one play per remaining round, plus the cards enemies are expected to strip.
/// </summary>
public sealed class KeepCardsToEndRule : IBotRule
{
    /// <summary>The most a single play uses up (Reallocate: four discards and the card it takes), plus slack.</summary>
    private const int MaxConsumption = 6;

    /// <summary>Cards kept back on top of the expected losses.</summary>
    private const int Reserve = 2;

    public string Name => "keep_cards_to_end";

    public string Description =>
        "Never spend extra cards (Reallocate, discard costs) if hand and deck would then run out before the last round";

    public double DefaultWeight => 1.0;

    public bool EnabledByDefault => false;

    public IReadOnlySet<string> Kinds { get; } = new HashSet<string> { "HandCardPlay", "ActivateCard" };

    public IReadOnlySet<CliOptionKind> OptionKinds { get; } = new HashSet<CliOptionKind> { CliOptionKind.Card };

    public bool AppliesTo(BotDecision decision) => GameFlow.Instance != null;

    public void Apply(BotDecision decision, IBotVerdictSink sink)
    {
        Dictionary<Faction, int> spare = new();
        for (int i = 0; i < decision.Options.Count; i++)
        {
            CardState card = decision.Options[i].Kind == CliOptionKind.Card ? CardState.ForId(decision.Options[i].Id) : null;
            if (card == null) continue;
            if (!spare.TryGetValue(card.Faction, out int left)) spare[card.Faction] = left = Spare(card.Faction);
            if (left >= MaxConsumption) continue;   // nothing could break the budget: skip the projection

            // A normal play uses its own card, which the budget already counts; anything beyond it is extra.
            int free = decision.Spec.Kind == "HandCardPlay" ? 1 : 0;
            int? used = ProjectionValuer.CardsConsumed(card.Id);
            if (used is int u && u > free && u - free > left) sink.Veto(i, $"would run out of cards ({u} used, {left} spare)");
        }
    }

    /// <summary>Cards in hand and deck beyond one play per remaining round and the expected enemy deck discards.</summary>
    internal static int Spare(Faction faction)
    {
        GameFlow flow = GameFlow.Instance;
        int roundsLeft = Math.Max(flow.MaxRound - flow.Round + 1, 0);
        FactionRecord piles = BoardState.Live.ForFaction(faction);
        return piles.Hand.Count + piles.Deck.Count - roundsLeft - ExpectedLosses(faction, flow.Round, roundsLeft) - Reserve;
    }

    /// <summary>
    /// Deck cards enemy effects are expected to take over the rounds left: this game's rate so far, but never below
    /// <c>bot_deck_loss_rate</c> (0.4/round; sims show 5-8 a game), since the hits come in bursts of 4.
    /// </summary>
    private static int ExpectedLosses(Faction faction, int round, int roundsLeft)
    {
        double rate = CliArgs.GetDouble("bot_deck_loss_rate", 0.4);
        List<GameMessage> messages = MultiplayerSession.Instance?.GameState?.GameMessages;
        if (messages != null && round > 1)
        {
            FactionTeam team = StaticGameData.FactionTeamForFaction(faction);
            int lost = messages.OfType<ForceDiscardCardsChangeEvent>()
                .Where(e => e.TargetFaction == faction && StaticGameData.FactionTeamForFaction(e.TriggeringFaction) != team)
                .Sum(e => e.DiscardedCardIds.Count);
            rate = Math.Max(rate, lost / (double)(round - 1));
        }
        return (int)Math.Ceiling(rate * roundsLeft);
    }
}
