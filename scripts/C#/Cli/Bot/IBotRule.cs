using System.Collections.Generic;

/// <summary>
/// One piece of judgement about what the sim bot should do — "never play a card that cannot act",
/// "prefer a battle target that costs the enemy victory points".
///
/// A rule NEVER decides an answer. It records vetoes and preferences into an
/// <see cref="IBotVerdictSink"/>; <see cref="BotRuleEngine"/> decides which of them survive, and
/// <see cref="RandomInputProvider"/> makes the final pick uniformly among what is left. That split is
/// what keeps the sim a sampler: rules narrow the field, the dice still choose inside it.
///
/// Three hard constraints on an implementation, each of which has cost a run before:
///
///  1. **No randomness.** A rule has no access to a Random, must not create one, and must never touch
///     <see cref="GameRandom"/>. Every draw belongs to the provider's single counted decision stream
///     (see <see cref="RandomInputProvider.Draws"/>), and the only roll the engine makes on a rule's
///     behalf is the per-rule `suppress` skip, which is configuration rather than policy.
///
///     A rule wanting "do this sometimes" is a rule that has not decided what it thinks. Say it as a
///     score instead: a negative score is "prefer anything else, but this is still reachable when it
///     is all there is", which is what "sometimes" almost always means on inspection. The score is
///     then tunable from the command line, composes with other rules, and appears in the trace as a
///     ranking — where a coin flip appears as nothing at all, and leaves "did this rule do anything?"
///     unanswerable even in principle.
///  2. **No mutation.** Read game state; never call a <see cref="GameAPI"/> mutator, never apply a
///     ChangeEvent, never call GameStateCalculator.CalculateAll. Tags are already fresh at this point.
///  3. **No re-deriving legality.** The offer set is authoritative. If the game offered it, it is
///     legal — <see cref="InputRequestSpec"/> is the only place that decides that, and it serves the
///     interactive CLI and the tutorial as well as the bot.
///
/// Implementations are stateless singletons: one instance is built by <see cref="BotRuleRegistry"/>
/// and reused for the whole run, so a rule cannot leak one prompt's options into the next. Counters
/// live on the engine, keyed by <see cref="Name"/>, so telemetry costs a rule author nothing.
/// </summary>
public interface IBotRule
{
    /// <summary>
    /// Stable, arg-facing identity — lowercase with underscores, unique in the registry. Appears in
    /// <c>bot_rules=</c>, in the per-decision trace and as a row in <c>rule_stats.csv</c>, so renaming
    /// one invalidates a saved command line and breaks a batch comparison. Choose it once.
    /// </summary>
    string Name { get; }

    /// <summary>One line, shown by <c>bot_rules_list=true</c>. What it does, not how.</summary>
    string Description { get; }

    /// <summary>
    /// Scale applied to every score this rule reports, overridable per run with
    /// <c>bot_rules=name:2.0</c>. Meaningless for a pure veto rule, which should report 1.
    /// </summary>
    double DefaultWeight { get; }

    /// <summary>
    /// Whether the rule is on when <c>bot_rules</c> does not mention it. A rule that changes the bot's
    /// measured behaviour should default OFF until an A/B says it helps — the exception is
    /// <see cref="NoHollowRule"/>, which was measured before this engine existed.
    /// </summary>
    bool EnabledByDefault { get; }

    /// <summary>
    /// The <see cref="InputRequestSpec.Kind"/> values this rule speaks to — "HandCardPlay",
    /// "SelectBattleTarget", and so on. Null means every prompt, which is almost always wrong.
    ///
    /// Validated against the real request types at registry construction, so a typo is a boot failure
    /// rather than a rule that silently never fires. That matters more here than usual: the whole point
    /// of a configured policy is that nobody rebuilds to check it.
    /// </summary>
    IReadOnlySet<string> Kinds { get; }

    /// <summary>
    /// Which option kinds this rule may write verdicts about; null means all. The sink REFUSES writes
    /// to any other kind, which is defence in depth for SelectBattleTarget — the one prompt whose
    /// options span two kinds (countries and units), where a country rule silently vetoing a unit
    /// would be both wrong and very hard to see.
    /// </summary>
    IReadOnlySet<CliOptionKind> OptionKinds { get; }

    /// <summary>
    /// Dynamic gate, called after the <see cref="Kinds"/> match and before <see cref="Apply"/>. Keep it
    /// cheap — round, step, whether a card is asking. Returning false costs the rule nothing and is not
    /// counted as having been consulted.
    /// </summary>
    bool AppliesTo(BotDecision decision);

    /// <summary>
    /// Record this rule's opinion about the prompt. Called at most once per prompt — not once per
    /// option — so a rule that needs a board-wide scan pays for it once, and a ranking rule sees the
    /// whole option set at the same time, which is the only way to rank.
    /// </summary>
    void Apply(BotDecision decision, IBotVerdictSink sink);
}

/// <summary>
/// Where a rule writes its opinion. Indices address <see cref="BotDecision.Options"/> — the list as
/// the game offered it, before any veto — so two rules always mean the same thing by "option 3".
/// </summary>
public interface IBotVerdictSink
{
    /// <summary>
    /// "Do not take this." A veto is a guarantee, not a strong opinion — subject only to the engine's
    /// safety floor, which discards ALL of a prompt's vetoes rather than leave it unanswerable (see
    /// <see cref="BotRuleEngine"/>).
    ///
    /// Reach for <see cref="Score"/> instead unless the option is genuinely never worth taking. A veto
    /// that the floor keeps discarding is a rule that reports activity and changes nothing — watch the
    /// <c>floored</c> counter.
    /// </summary>
    void Veto(int optionIndex, string why = null);

    /// <summary>
    /// "Prefer this" (positive) or "prefer this less" (negative). Additive across rules and scaled by
    /// the rule's weight. The engine picks uniformly among the top-scoring options, so a score is a
    /// ranking, not a probability.
    /// </summary>
    void Score(int optionIndex, double points);

    /// <summary>
    /// "Nothing here is worth doing." Cannot force a pass on a prompt that forbids one — the structural
    /// checks in ShouldPass run first.
    /// </summary>
    void ForcePass(string why = null);

    /// <summary>
    /// "This prompt must be answered." Beats <see cref="ForcePass"/> on purpose: wrongly forcing a pass
    /// is silent passivity, which is the failure this bot was built to avoid, while wrongly refusing
    /// one is a bad play that shows up in the statistics.
    /// </summary>
    void RefusePass(string why = null);
}

/// <summary>One rule's resolved settings for this run, after <c>bot_rules</c> has been parsed.</summary>
public sealed class BotRuleConfig
{
    public bool Enabled;
    public double Weight;

    /// <summary>
    /// Chance per prompt of skipping this rule entirely, rolled by the ENGINE (never by the rule).
    ///
    /// The escape hatch that lets one build serve both purposes: a balance run wants the bot to play
    /// sensibly, while a fuzzing run wants the branches a sensible bot never takes — a hollow rebuild
    /// still raises a reactable DeployUnitChangeEvent, and somebody has to walk it. <c>bot_hollow</c> is
    /// sugar for this on <see cref="NoHollowRule"/>.
    /// </summary>
    public double Suppress;
}
