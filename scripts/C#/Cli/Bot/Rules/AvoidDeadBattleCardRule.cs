using System.Collections.Generic;

/// <summary>
/// Refuse to PLAY a battle card when there is no enemy unit it could fight.
///
/// The stricter half of the pair, and the one that actually saves the card.
/// <see cref="AvoidEmptyBattleRule"/> can only choose the least-bad target once the play is already
/// spent; this declines the play while it is still worth something. A Land Battle with no attackable
/// enemy army can do exactly one thing — battle an empty space, which
/// BattleCountryChangeEvent.ExecuteAsync resolves as <c>await Task.CompletedTask; return true;</c> —
/// so playing it converts a card into nothing.
///
/// Same two cases as its sibling, from the same predicate, so the two rules cannot disagree about the
/// board:
///
///   nothing on the table  → VETO. The card can achieve literally nothing.
///   something on the table → SCORE it down. The battle would still fire this faction's triggers, so
///                            it is a weak play rather than a wasted one — worth making only when
///                            nothing better is in hand.
///
/// Vetoing a card is safe in a way vetoing a battle TARGET is not: the play prompt's MinSelections is
/// 0 and its pass is an ordinary answer, so the engine's floor only steps in when passing would cost
/// something — and there the floor's judgement is right, since paying a victory point to avoid wasting
/// a card is the worse trade.
///
/// Scoped by <see cref="CardData.CardType"/>, which is data-driven and already shipped, so this covers
/// every LAND_BATTLE / SEA_BATTLE card in the game without touching a card file and without keying on
/// a card name. That is also why it is not redundant with no_hollow: Tag.NeedsAttention needs the card
/// to DECLARE advisory conditions, and no battle card declares any.
///
/// KNOWN GAP: an EVENT card that happens to contain a battle step is invisible here, because its
/// CardType is EVENT. EventGunsandButter is the clearest case — its own gate is
/// <c>AttackableArmies.Any() || AttackableLand.Any()</c>, so it offers a Land Battle branch when only
/// empty spaces are available. Those plays are what leave AvoidEmptyBattleRule facing a prompt of
/// nothing but empty spaces, which shows up as its <c>floored</c> count. Closing it needs a per-card
/// marker or the prompt-origin work, not a bigger CardType switch.
/// </summary>
public sealed class AvoidDeadBattleCardRule : IBotRule
{
    /// <summary>
    /// How hard to push away from a battle card that can only manage a trigger. Deliberately below
    /// <see cref="AvoidEmptyBattleRule"/>'s: this competes against every other card in hand, where a
    /// merely weak play should still beat paying the pass penalty.
    /// </summary>
    private const double FallbackPenalty = -4;

    public string Name => "avoid_dead_battle_card";

    public string Description =>
        "Never play a battle card with no enemy unit to attack and nothing in play to trigger";

    public double DefaultWeight => 1.0;

    public bool EnabledByDefault => false;

    /// <summary>
    /// The prompts that put a card into play. BlockReaction is excluded: a block is answering someone
    /// else's event under time pressure, and a battle card is not a block.
    /// </summary>
    public IReadOnlySet<string> Kinds { get; } = new HashSet<string>
    {
        "HandCardPlay", "ActivateCard",
    };

    public IReadOnlySet<CliOptionKind> OptionKinds { get; } = new HashSet<CliOptionKind>
    {
        CliOptionKind.Card,
    };

    public bool AppliesTo(BotDecision decision) => true;

    public void Apply(BotDecision decision, IBotVerdictSink sink)
    {
        bool couldFireATrigger = BotBattleFacts.HasTriggerableTableCards(decision.Faction);

        for (int i = 0; i < decision.Options.Count; i++)
        {
            CardState card = CardState.ForId(decision.Options[i].Id);
            if (card == null) continue;

            CardType type = card.CardData.CardType;
            if (!BotBattleFacts.IsBattleCard(type)) continue;
            if (BotBattleFacts.HasRealTarget(type, decision.Faction)) continue;

            if (couldFireATrigger) sink.Score(i, FallbackPenalty);
            else                   sink.Veto(i, "no unit to attack, nothing to trigger");
        }
    }
}
