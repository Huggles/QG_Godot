namespace QGSim;

/// <summary>
/// One unattended game to run: exactly the inputs a single <c>sim=true</c> Godot process takes.
///
/// The pair that matters is (<see cref="Seed"/>, <see cref="DecisionSeed"/>). Seed deals the board
/// and the decks; DecisionSeed drives the bot's choices. They are separate RNGs on purpose - fixing
/// the seed and varying the decision seed replays the same opening position under different play,
/// which is the shape a balance sample needs.
/// </summary>
public sealed record SimJob(
    string Scenario,
    int Seed,
    int DecisionSeed,
    double BotPass,
    double BotDiscard)
{
    /// <summary>Stable, filesystem-safe identity. Also the sort key that makes a run diffable.</summary>
    public string Id => $"{Scenario}_s{Seed}_d{DecisionSeed}";

    /// <summary>
    /// The user args Godot passes through after <c>--</c>.
    ///
    /// <c>json=true</c> is not optional here: text mode is for humans, and the orchestrator parses
    /// one JSON object per line. Verbose is deliberately absent - a verbose full game is thousands of
    /// lines, and at batch scale that is the whole disk budget.
    /// </summary>
    public IEnumerable<string> ToUserArgs()
    {
        yield return "cli=true";
        yield return "sim=true";
        yield return "json=true";
        yield return $"seed={Seed}";
        yield return $"decision_seed={DecisionSeed}";
        yield return $"scenario={Scenario}";
        yield return $"bot_pass={BotPass.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        yield return $"bot_discard={BotDiscard.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }
}

/// <summary>
/// How a run ended. Mirrors the CLI's exit codes rather than collapsing them, because the three
/// failure shapes need different responses: a rules error is a bug to read, a stall is a run to
/// discard, a timeout is a process that never even got to its own watchdog.
/// </summary>
public enum SimOutcome
{
    /// <summary>exit 0 - reached a win condition, result emitted, no errors.</summary>
    Clean,

    /// <summary>exit 2 - a result WAS produced, but rules errors were seen getting there.</summary>
    ResultWithErrors,

    /// <summary>exit 3 - the in-game watchdog fired: no result.</summary>
    Stalled,

    /// <summary>Killed by the orchestrator's own clock, before the in-game watchdog could fire.</summary>
    Timeout,

    /// <summary>Process failed to launch, crashed, or exited with a code the CLI never emits.</summary>
    Crashed,
}

/// <summary>
/// One faction's victory points across the rounds of a single game, straight from the run's
/// <c>round_scores</c> event. Both series are indexed by round 1..N at offset 0..N-1 and are
/// zero-filled by the emitter, so a round the faction did not score in reads 0 rather than being
/// absent — an average over these needs no gap handling.
/// </summary>
public sealed class RoundSeries
{
    public required string Team { get; init; }

    /// <summary>Points scored IN each round. Can be negative — a forced discard on an empty deck costs a VP.</summary>
    public required int[] Deltas { get; init; }

    /// <summary>Running total AT THE END OF each round.</summary>
    public required int[] Totals { get; init; }
}

/// <summary>The outcome of one job: the parsed <c>game_result</c> plus how the process itself fared.</summary>
public sealed class SimResult
{
    public required SimJob Job { get; init; }
    public required SimOutcome Outcome { get; init; }
    public required int ExitCode { get; init; }

    /// <summary>Wall-clock as measured by the orchestrator. Includes Godot boot, unlike sim_perf.</summary>
    public required long WallMs { get; init; }

    /// <summary>Where this run's raw stdout landed. Always written; kept per the --keep policy.</summary>
    public required string LogPath { get; init; }

    /// <summary>The raw <c>game_result</c> JSON line, or null when no result was produced.</summary>
    public string? ResultJson { get; init; }

    public string? Winner { get; init; }
    public int AxisTotal { get; init; }
    public int AlliesTotal { get; init; }
    public int FinalRound { get; init; }
    public string? EndReason { get; init; }
    public int Prompts { get; init; }
    public int Errors { get; init; }
    public int Resumes { get; init; }
    public Dictionary<string, int> Factions { get; init; } = new();

    /// <summary>First <c>game_error</c> message seen, for the failure summary.</summary>
    public string? FirstError { get; init; }

    /// <summary>Per-faction VP by round, keyed by faction name. Empty when the run emitted no result.</summary>
    public Dictionary<string, RoundSeries> Rounds { get; init; } = new();

    public bool HasResult => ResultJson != null;
}
