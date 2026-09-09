using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Consults the enabled <see cref="IBotRule"/>s about a prompt and rules on what they said.
///
/// The one invariant this class exists to enforce, and the reason vetoes are collected here rather
/// than applied by the rules themselves:
///
///   **A veto is a preference, and a preference never makes a prompt unanswerable.** If honouring the
///   vetoes would leave no legal answer, ALL of the prompt's vetoes are discarded and the original
///   option list is restored — scores are kept, so the bot still answers in the least-bad order.
///
/// All-or-nothing rather than relaxing vetoes one at a time. Relaxing needs a priority order over
/// vetoes, which would force every rule author to reason about how their veto ranks against every
/// other rule's under scarcity, and would produce the least diagnosable behaviour available: a veto
/// honoured except on the turns when it mattered. A discarded set is one counter (Floored) and one
/// obvious remedy (write it as a score instead).
///
/// Two further properties are load-bearing:
///
///  1. Rules are consulted in a fixed LIST order, never a Dictionary's or a HashSet's enumeration.
///     Floating-point addition is not associative, so a different rule order changes a score total in
///     its last bit, which can flip a tier boundary and silently change a pick. Lookup by name may use
///     a dictionary; iteration may not.
///  2. The engine owns every random draw. It takes a draw delegate from the provider rather than
///     holding a Random, so all draws stay on the provider's single counted decision stream (see
///     <see cref="RandomInputProvider.Draws"/>) and no rule can reach one.
/// </summary>
public sealed class BotRuleEngine
{
    /// <summary>Two score totals within this of each other are one tier. They are doubles: 0.1*3 != 0.3.</summary>
    public const double ScoreTolerance = 1e-9;

    private readonly List<IBotRule> _rules;                       // ORDERED. See note 1 above.
    private readonly Dictionary<string, BotRuleConfig> _config;
    private readonly Dictionary<string, BotRuleStats> _stats;
    private readonly HashSet<string> _failed = new();
    private readonly Func<double> _draw;
    private readonly double _tierWidth;

    /// <summary>Prompts where the safety floor discarded the vetoes. Should be near zero.</summary>
    public int SafetyOverrides { get; private set; }

    public IReadOnlyList<IBotRule> Rules => _rules;
    public BotRuleConfig ConfigFor(string name) => _config[name];
    public BotRuleStats StatsFor(string name) => _stats[name];

    /// <summary>Whether any rule is enabled at all. Lets the provider skip the engine entirely.</summary>
    public bool AnyEnabled { get; }

    /// <param name="draw">The provider's counted 0..1 draw. The engine's only source of randomness.</param>
    /// <param name="tierWidth">
    /// How far below the best total an option may score and still be eligible (<c>bot_rule_temp</c>).
    /// 0 means strict: only the top tier is picked from. Raising it turns a weak preference back into a
    /// nudge, which matters because several strong rules compounding narrow the sampler more than any
    /// one of them does — and narrowing the sampler is the cost of this whole engine.
    /// </param>
    public BotRuleEngine(List<IBotRule> rules, Dictionary<string, BotRuleConfig> config,
                         Func<double> draw, double tierWidth)
    {
        _rules = rules;
        _config = config;
        _draw = draw;
        _tierWidth = tierWidth;
        _stats = rules.ToDictionary(r => r.Name, r => new BotRuleStats { Name = r.Name });
        AnyEnabled = rules.Any(r => config[r.Name].Enabled);
    }

    // ── Consulting ───────────────────────────────────────────────────────────

    public BotAdvice Consult(BotDecision decision)
    {
        BotAdvice advice = new(decision.Options);

        foreach (IBotRule rule in _rules)
        {
            BotRuleConfig config = _config[rule.Name];
            if (!config.Enabled || _failed.Contains(rule.Name)) continue;
            if (rule.Kinds != null && !rule.Kinds.Contains(decision.Spec.Kind)) continue;

            BotRuleStats stats = _stats[rule.Name];

            if (!Guarded(rule, stats, () => rule.AppliesTo(decision))) continue;
            stats.Considered++;

            // Guarded on > 0 so a default run burns no draw at all: the suppression facility must cost
            // nothing until somebody turns it on, or every existing decision_seed would silently mean a
            // different game than it did before this engine landed.
            if (config.Suppress > 0 && _draw() < config.Suppress)
            {
                stats.Suppressed++;
                continue;
            }

            advice.BeginRule(rule, config.Weight);
            Guarded(rule, stats, () => { rule.Apply(decision, advice); return true; });

            if (advice.EndRule()) stats.Fired++;

            // A rule is consulted at most once per prompt and the advice is per-prompt, so these are
            // this prompt's totals for this rule — no running delta to track.
            stats.Vetoed += advice.VetoesBy(rule.Name);
            stats.Scored += advice.ScoresBy(rule.Name);
        }

        if (advice.PassBy != null && _stats.TryGetValue(advice.PassBy, out BotRuleStats passer))
        {
            if (advice.RefusesPass) passer.RefusedPass++;
            else if (advice.ForcesPass) passer.ForcedPass++;
        }

        return advice;
    }

    /// <summary>
    /// Run a rule's code so that a throw costs the rule and not the run.
    ///
    /// Needed because a rule reads game state by id, and <see cref="UnitState.ForId"/> uses a raw
    /// indexer that THROWS on an id that is no longer there — a unit removed by an intervening nested
    /// reaction is a perfectly ordinary way for that to happen. An exception escaping here would climb
    /// out through Resolve into the turn loop and end the run as a stall at exit 3: a whole game
    /// discarded because one rule guessed wrong about one id.
    ///
    /// Reported once, then the rule is disabled for the rest of the game — a broken rule at several
    /// hundred prompts a game would otherwise bury the log in identical reports. The Failed counter is
    /// what makes that silent disabling visible afterwards.
    /// </summary>
    private bool Guarded(IBotRule rule, BotRuleStats stats, Func<bool> body)
    {
        try
        {
            return body();
        }
        catch (Exception e)
        {
            stats.Failed++;
            _failed.Add(rule.Name);
            ErrorReporter.Report(e, $"bot rule \"{rule.Name}\" (disabled for the rest of this run)",
                                 Faction.NONE);
            ErrorReporter.MarkReported(e);
            return false;
        }
    }

    // ── Ruling ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The options that survive this prompt's vetoes, each paired with its score — or every option,
    /// scores intact, when honouring the vetoes would leave the prompt unanswerable.
    ///
    /// Returns options WITH their scores rather than letting the caller re-index after the option list
    /// is narrowed: the score array is indexed by pre-veto position, so re-indexing afterwards is a
    /// silent off-by-N that would misattribute every preference on any prompt carrying a veto.
    /// </summary>
    public List<ScoredOption> Survivors(BotDecision decision, BotAdvice advice)
    {
        List<ScoredOption> all = new(decision.Options.Count);
        for (int i = 0; i < decision.Options.Count; i++)
            all.Add(new ScoredOption(decision.Options[i], advice.ScoreAt(i)));

        if (!advice.AnyVetoes) return all;

        List<ScoredOption> kept = new(all.Count);
        for (int i = 0; i < all.Count; i++)
            if (!advice.IsVetoed(i)) kept.Add(all[i]);

        if (CanAnswer(decision.Spec, decision.Request, kept.Count)) return kept;

        advice.MarkFloored();
        SafetyOverrides++;
        foreach (string name in advice.RulesThatVetoed) _stats[name].Floored++;
        return all;
    }

    /// <summary>
    /// Whether a prompt offering <paramref name="keptCount"/> options still has a legal answer.
    ///
    /// One predicate rather than a branch per request type, and it covers all of them:
    ///
    ///  - A mandatory prompt (ForceDiscardHandCards, a required SelectCard, SelectUnit with AllowSkip
    ///    false) reports <see cref="PassMode.NotAllowed"/>, so CanPass is false and the zero-branch
    ///    cannot fire; MinSelections is then the binding constraint.
    ///  - A minimum-selection prompt (SelectCards) is caught by the same first clause, which is written
    ///    on MinSelections rather than on PassMode so it keeps holding if a future spec row pairs
    ///    EmptyResponse with a nonzero minimum.
    ///  - ReorderCards has MinSelections == every option, so vetoing ANY option fails the first clause
    ///    and restores the list. Vetoes are therefore inert on reorder prompts, which is the right
    ///    reading of a prompt whose answer is an ORDER rather than a subset — rules steer it by score.
    ///  - A pass that COSTS something (PassCostText: the faction's own play prompt charges a discard or
    ///    a victory point) is not a free answer, so an empty survivor set is not answerable. This is
    ///    the clause the hollow-card filter carried before this engine existed, generalised: playing a
    ///    pointless card beats paying a victory point to avoid it.
    /// </summary>
    private static bool CanAnswer(InputRequestSpec spec, InputRequest request, int keptCount)
    {
        if (keptCount > 0 && keptCount >= spec.MinSelections) return true;

        return keptCount == 0
            && spec.CanPass
            && spec.MinSelections == 0
            && spec.Pass != PassMode.EchoTargets
            && request.PassCostText == null;
    }

    /// <summary>
    /// The surviving options grouped into descending preference tiers: equal-scoring options land
    /// together and the caller picks uniformly inside a tier, which is what keeps the bot a sampler
    /// rather than a script.
    ///
    /// Order within a tier is the game's own offer order, so a single-tier prompt hands back exactly
    /// the list the provider would otherwise have shuffled.
    /// </summary>
    public List<List<CliOption>> Tiers(List<ScoredOption> scored)
    {
        List<ScoredOption> ordered = scored.OrderByDescending(s => s.Score).ToList();
        List<List<CliOption>> tiers = new();
        double tierTop = 0;

        foreach (ScoredOption option in ordered)
        {
            if (tiers.Count == 0 || tierTop - option.Score > Math.Max(ScoreTolerance, _tierWidth))
            {
                tiers.Add(new List<CliOption>());
                tierTop = option.Score;
            }
            tiers[^1].Add(option.Option);
        }

        return tiers;
    }
}

/// <summary>An option and the total preference every rule assigned it.</summary>
public readonly struct ScoredOption
{
    public readonly CliOption Option;
    public readonly double Score;
    public ScoredOption(CliOption option, double score) { Option = option; Score = score; }
}

/// <summary>
/// One rule's counters for one game, emitted as a row of <c>bot_rules_stats</c>.
///
/// <see cref="Considered"/> is the field that makes the rest interpretable: "fired 41 times" says
/// nothing without it, because 41 of 86 relevant prompts is a rule that matters and 41 of 4000 is
/// noise.
/// </summary>
public sealed class BotRuleStats
{
    public string Name;

    /// <summary>Prompts where this rule was applicable and got asked.</summary>
    public int Considered;

    /// <summary>Of those, the ones where it actually had something to say.</summary>
    public int Fired;

    public int Vetoed;
    public int Scored;
    public int ForcedPass;
    public int RefusedPass;

    /// <summary>Prompts where the suppression roll skipped it. Makes <c>bot_hollow=0.3</c> auditable.</summary>
    public int Suppressed;

    /// <summary>
    /// Prompts where this rule's vetoes were discarded by the safety floor. Compare against
    /// <see cref="Fired"/>: if they are close, the rule is not doing what its author thinks it does.
    /// </summary>
    public int Floored;

    /// <summary>Times the rule threw and was disabled. Non-zero means read the log.</summary>
    public int Failed;
}
