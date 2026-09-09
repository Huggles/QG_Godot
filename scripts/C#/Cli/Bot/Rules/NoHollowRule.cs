using System.Collections.Generic;

/// <summary>
/// Refuse a card that is legal to play but whose every executable step would achieve nothing on the
/// board in front of it — the canonical case being a build card whose only remaining targets are
/// countries the faction already occupies, where the deploy rebuilds in place and the board is
/// identical afterwards.
///
/// Reads <see cref="Tag.NeedsAttention"/>, the replicated tag GameStateCalculator raises from the
/// steps' advisory conditions, rather than judging hollowness itself. That is the point: it is the same
/// signal the player's hand draws its caution scrim from, so the bot declines exactly what a human is
/// warned about, and the two cannot drift apart as more cards gain advisory conditions.
///
/// On by default, alone among the rules — it was measured against a 200-game baseline before this
/// engine existed, and the migration into the engine is required to reproduce that baseline byte for
/// byte. <c>bot_hollow=X</c> remains as sugar for <c>bot_rules=no_hollow:suppress=X</c>.
///
/// Two behaviours that USED to live in this rule's own code and are now the engine's:
///
///  - "Leave the list alone when every option is hollow and passing costs something" is the engine's
///    safety floor (BotRuleEngine.CanAnswer), which reaches the same answer for the same reason —
///    playing a pointless card beats paying a victory point to avoid it.
///  - "Where passing is free, empty the list and let that become the pass" also falls out of the
///    floor: an empty survivor set IS answerable when a pass costs nothing, so it stands and
///    ShouldPass turns it into the pass.
/// </summary>
public sealed class NoHollowRule : IBotRule
{
    public string Name => "no_hollow";

    public string Description =>
        "Never play a card whose every effect would be hollow on the current board (Tag.NeedsAttention)";

    public double DefaultWeight => 1.0;

    public bool EnabledByDefault => true;

    /// <summary>
    /// The three prompts that PLAY or ACTIVATE a card.
    ///
    /// A discard prompt is excluded because it asks the opposite question — a hollow card is the one you
    /// WANT to throw away — and the other prompts never carry the tag anyway, since
    /// GameStateCalculator only raises it on cards already marked <see cref="Tag.IsActivatable"/>.
    /// Naming the three is still better than relying on that: the rule then states its own scope
    /// instead of depending on a decision made in another file.
    /// </summary>
    public IReadOnlySet<string> Kinds { get; } = new HashSet<string>
    {
        "HandCardPlay", "ActivateCard", "BlockReaction",
    };

    public IReadOnlySet<CliOptionKind> OptionKinds { get; } = new HashSet<CliOptionKind>
    {
        CliOptionKind.Card,
    };

    public bool AppliesTo(BotDecision decision) => true;

    public void Apply(BotDecision decision, IBotVerdictSink sink)
    {
        for (int i = 0; i < decision.Options.Count; i++)
        {
            CliOption option = decision.Options[i];
            if (option.Kind != CliOptionKind.Card) continue;

            // Queried for the asked faction, which is also the owner of every card these three prompts
            // offer — the tag is raised per owning faction.
            if (CardState.ForId(option.Id)?.HasTag(Tag.NeedsAttention, decision.Faction) == true)
                sink.Veto(i, "hollow");
        }
    }
}
