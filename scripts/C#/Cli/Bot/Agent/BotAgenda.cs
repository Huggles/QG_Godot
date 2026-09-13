using System.Collections.Generic;

/// <summary>
/// What one faction wants right now, in priority order, with a board read attached.
///
/// This is the piece the bot has never had. Rules answer one prompt and forget it —
/// <see cref="BotDecision.Scratch"/> is discarded the moment a prompt is answered — so an intention could
/// not previously survive from "which card do I play" to "and where do I put the unit it just gave me".
/// The agenda is that missing memory, rebuilt from the board at each decision point rather than carried
/// forward, so it can never go stale against a board a nested reaction has changed underneath it.
///
/// **Nothing reads this yet.** No <see cref="IBotRule"/> consults it and no decision depends on it; the
/// bot plays exactly as it did before this existed. That is deliberate. It lands the perception layer
/// where it can be inspected in the trace and judged on whether it describes the board correctly, before
/// any behaviour is staked on it — and without depending on the card system, which is being reworked.
///
/// **Determinism is the constraint that shapes every choice here.** The bot's reproducibility rests on a
/// single counted RNG stream owned by <see cref="RandomInputProvider"/>; the agenda must be a pure
/// function of the board or the same (seed, decision_seed) stops reproducing the same game. So: proposers
/// run in a fixed array order, each walks lists that are in stable board order, and the final sort is
/// total — no tie anywhere is left to be broken by hash order, dictionary enumeration order, or the order
/// units happened to be created.
/// </summary>
public sealed class BotAgenda
{
    /// <summary>
    /// The proposers, in consult order.
    ///
    /// Fixed order for the same reason <see cref="BotRuleRegistry.All"/> is a List and not a set: the
    /// values below are floating point and addition is not associative, so reordering this array can
    /// change the agenda. Append; do not insert.
    /// </summary>
    private static readonly IGoalProposer[] Standard =
    {
        new TakeEmptyStarProposer(),
        new ClearEnemyStarProposer(),
        new HoldThreatenedStarProposer(),
        new ExtendSupplyReachProposer(),
        new KeepUnitsInSupplyProposer(),
    };

    private static readonly List<Goal> NoGoals = new();

    /// <summary>The board read the goals were derived from. Null only if taking the read itself failed.</summary>
    public BoardAssessment Assessment { get; }

    /// <summary>Goals, best first. Empty is a normal answer, not a failure.</summary>
    public IReadOnlyList<Goal> Goals { get; }

    public BotProfile Profile { get; }

    private BotAgenda(BoardAssessment assessment, IReadOnlyList<Goal> goals, BotProfile profile)
    {
        Assessment = assessment;
        Goals = goals ?? NoGoals;
        Profile = profile;
    }

    /// <summary>The single most valuable thing on the board for this faction, or null if it wants nothing.</summary>
    public Goal? Top => Goals.Count > 0 ? Goals[0] : null;

    /// <summary>
    /// Build the agenda for a faction.
    ///
    /// Never throws. Perception failing must cost the bot its opinion and nothing else: a prompt still
    /// has to be answered, and an exception escaping here would surface as a stalled run (CliSimRunner
    /// exit 3) rather than as a bot that merely played less well. Each proposer is guarded individually
    /// for the same reason <see cref="BotRuleEngine"/> guards each rule — state reads by id can throw
    /// when a nested reaction has removed the thing being read, and one proposer tripping over that
    /// should not cost the other four their goals.
    /// </summary>
    public static BotAgenda Build(Faction faction, BotProfile profile)
    {
        profile ??= BotProfile.Generic();

        BoardAssessment assessment;
        try { assessment = BoardAssessment.For(faction); }
        catch { return new BotAgenda(null, NoGoals, profile); }

        List<Goal> goals = new();
        RunAll(Standard, assessment, profile, goals);
        if (profile.ExtraProposers != null && profile.ExtraProposers.Count > 0)
            RunAll(profile.ExtraProposers, assessment, profile, goals);

        goals.Sort(CompareGoals);
        return new BotAgenda(assessment, goals, profile);
    }

    private static void RunAll(IReadOnlyList<IGoalProposer> proposers, BoardAssessment assessment,
                               BotProfile profile, List<Goal> into)
    {
        for (int i = 0; i < proposers.Count; i++)
        {
            IGoalProposer proposer = proposers[i];
            if (proposer == null) continue;

            try { proposer.Propose(assessment, profile.WeightFor(proposer.Kind), into); }
            catch { /* see Build: one proposer's failure costs only its own goals */ }
        }
    }

    /// <summary>
    /// Best first, and TOTAL — every field is compared, down to the reason string.
    ///
    /// List.Sort is introsort and unstable, so leaving any pair of goals mutually "equal" would let their
    /// relative order fall out of the pivot layout rather than out of the board. That would be invisible
    /// in a single game and would show up as an unreproducible trace, which is the one thing the whole
    /// determinism discipline exists to prevent.
    /// </summary>
    private static int CompareGoals(Goal a, Goal b)
    {
        int byValue = b.Value.CompareTo(a.Value);
        if (byValue != 0) return byValue;

        int byKind = ((int)a.Kind).CompareTo((int)b.Kind);
        if (byKind != 0) return byKind;

        int byCountry = a.TargetCountryId.CompareTo(b.TargetCountryId);
        if (byCountry != 0) return byCountry;

        return string.CompareOrdinal(a.Why ?? "", b.Why ?? "");
    }

    /// <summary>
    /// Total goal value pointing at one country.
    ///
    /// A sum rather than a max: a star that is both worth taking and worth denying to the enemy is
    /// genuinely worth more than either reason alone, and because every value is in victory points they
    /// can legitimately be added. Callers arrive later — this is the shape a rule reading the agenda will
    /// want.
    /// </summary>
    public double ValueFor(int countryId)
    {
        double total = 0;
        for (int i = 0; i < Goals.Count; i++)
            if (Goals[i].TargetCountryId == countryId) total += Goals[i].Value;
        return total;
    }

    /// <summary>How many goals of a kind survived into the agenda. For the per-kind trace counters.</summary>
    public int CountOf(GoalKind kind)
    {
        int count = 0;
        for (int i = 0; i < Goals.Count; i++) if (Goals[i].Kind == kind) count++;
        return count;
    }

    /// <summary>
    /// Compact trace form: the board headline plus the best few goals. Emitted per prompt, so it is kept
    /// short and must be byte-stable for a given (seed, decision_seed) — that reproducibility is what
    /// makes the trace usable as a diff.
    /// </summary>
    public string Headline
    {
        get
        {
            string board = Assessment?.Headline ?? "no assessment";
            if (Goals.Count == 0) return board + " | goals: none";

            System.Text.StringBuilder sb = new();
            sb.Append(board).Append(" | goals(").Append(Goals.Count).Append("):");

            int shown = Goals.Count < 3 ? Goals.Count : 3;
            for (int i = 0; i < shown; i++)
            {
                Goal goal = Goals[i];
                sb.Append(" ").Append(goal.Kind).Append("@")
                  .Append(CountryState.ForId(goal.TargetCountryId)?.Label ?? goal.TargetCountryId.ToString())
                  .Append("=").Append(goal.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
