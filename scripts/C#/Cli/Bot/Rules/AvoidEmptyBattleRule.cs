using System.Collections.Generic;

/// <summary>
/// Refuse to attack an empty space, which changes nothing on the board.
///
/// The board no-op is exact, not a judgement call. AttackOption.CalculateAttackOptions adds a COUNTRY
/// to a battle prompt's options only when its OccupyingTeam is NONE, so a country option always means
/// an empty space; BattleTarget then builds a plain BattleCountryChangeEvent, whose ExecuteAsync is
/// <c>await Task.CompletedTask; return true;</c> — no unit removed, no occupation, no score. Meanwhile
/// every empty space adjacent to every supplied unit is offered, so these are usually the MAJORITY of
/// a Land Battle prompt's options: uniform random was spending a large share of the game's battle
/// cards on nothing.
///
/// The one thing the attack still does is set IsBattle, which fires Condition.HasBattledOnLand /
/// HasBattledAtSea / FactionBattled and the Response cards built on them. That is only worth anything
/// if the faction has a card on the table waiting for it, which is the question
/// <see cref="BotBattleFacts.HasTriggerableTableCards"/> asks — and it is the whole of the difference
/// between this rule's two cases:
///
///   nothing on the table  → VETO. The attack cannot achieve anything at all.
///   something on the table → SCORE it down. A real target is still better, but the empty space is a
///                            legitimate fallback rather than something to forbid.
///
/// The second case is a score and not "no opinion" on purpose. Abstaining would leave the empty
/// spaces competing on equal terms with the units, and since they are usually the majority of the
/// options the bot would then attack an empty space MOST of the time whenever it held any table card
/// — which is the behaviour this rule exists to stop. A score says the actual policy: prefer a real
/// target whenever one exists, take the trigger when it is all that is on offer.
///
/// When EVERY option is an empty space and the veto applies, the engine's safety floor discards it and
/// the bot attacks one anyway. That is the right outcome rather than a leak: by then the card is
/// already spent, and an empty-space battle at least fires the battle triggers, whereas skipping the
/// target would waste the play entirely. Not playing the card in the first place is
/// <see cref="AvoidDeadBattleCardRule"/>'s job, and that is where the strictness belongs.
/// </summary>
public sealed class AvoidEmptyBattleRule : IBotRule
{
    /// <summary>
    /// How hard to push away from an empty space that could still fire a trigger. Large enough that a
    /// real target always outranks it, small enough to stay a preference. Scaled by the rule's weight,
    /// so <c>bot_rules=avoid_empty_battle:2</c> doubles it.
    /// </summary>
    private const double FallbackPenalty = -5;

    public string Name => "avoid_empty_battle";

    public string Description =>
        "Never attack an empty space with no Status/Response card in play; otherwise prefer a real target";

    public double DefaultWeight => 1.0;

    /// <summary>
    /// Off until an A/B says otherwise, unlike no_hollow. It changes what the bot does on a very
    /// common prompt, so it must not become the silent default of a measurement nobody re-ran.
    /// </summary>
    public bool EnabledByDefault => false;

    public IReadOnlySet<string> Kinds { get; } = new HashSet<string> { "SelectBattleTarget" };

    /// <summary>
    /// Country options only. SelectBattleTarget is the one prompt whose options span two kinds, and
    /// the whole rule turns on that distinction: a Country option is an empty space, a Unit option is
    /// a real enemy piece. The sink enforces this, so the rule cannot reach a unit by accident.
    /// </summary>
    public IReadOnlySet<CliOptionKind> OptionKinds { get; } = new HashSet<CliOptionKind>
    {
        CliOptionKind.Country,
    };

    public bool AppliesTo(BotDecision decision) => true;

    public void Apply(BotDecision decision, IBotVerdictSink sink)
    {
        // Read once for the prompt, not once per option: it is a fact about the faction's table, and
        // nothing in this loop changes it.
        bool couldFireATrigger = BotBattleFacts.HasTriggerableTableCards(decision.Faction);

        for (int i = 0; i < decision.Options.Count; i++)
        {
            if (decision.Options[i].Kind != CliOptionKind.Country) continue;

            if (couldFireATrigger) sink.Score(i, FallbackPenalty);
            else                   sink.Veto(i, "empty space, nothing to trigger");
        }
    }
}
