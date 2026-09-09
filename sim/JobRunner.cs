using System.Diagnostics;
using System.Text.Json;

namespace QGSim;

/// <summary>
/// Runs one <see cref="SimJob"/> as its own headless Godot process and parses what it printed.
///
/// One process per game is not a design preference, it is forced. The game's state lives in
/// process-global statics - GameRandom's static Random and DrawCount, InputServices' single provider
/// slot, twelve Godot autoloads, one SceneTree - so two games in one process would interleave their
/// RNG draws and stop reproducing from (seed, decision_seed). Reproducibility is the entire point of
/// the sample, so the isolation boundary has to be the process.
/// </summary>
public sealed class JobRunner
{
    private readonly SimConfig _config;

    public JobRunner(SimConfig config) => _config = config;

    public async Task<SimResult> RunAsync(SimJob job, CancellationToken shutdown)
    {
        string logPath = Path.Combine(_config.LogDirectory, job.Id + ".jsonl");
        Stopwatch clock = Stopwatch.StartNew();

        ProcessStartInfo info = new()
        {
            FileName = _config.GodotPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // The sim answers nothing on stdin, but Godot still inherits a console handle if we let
            // it. Redirecting and immediately closing is the portable equivalent of `< /dev/null`.
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        info.ArgumentList.Add("--headless");
        info.ArgumentList.Add("--path");
        info.ArgumentList.Add(_config.ProjectPath);
        info.ArgumentList.Add("--");
        foreach (string arg in job.ToUserArgs()) info.ArgumentList.Add(arg);

        ResultAccumulator parsed = new();

        using Process proc = new() { StartInfo = info, EnableRaisingEvents = true };

        // Write every line through as it arrives rather than buffering the run in memory: a verbose
        // or error-spewing run can reach hundreds of KB, and at batch scale that is worth not holding.
        await using StreamWriter log = new(logPath, append: false);

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            log.WriteLine(e.Data);
            parsed.Feed(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            log.WriteLine("[stderr] " + e.Data);
        };

        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            await log.WriteLineAsync("[orchestrator] failed to launch: " + ex.Message);
            return new SimResult
            {
                Job = job,
                Outcome = SimOutcome.Crashed,
                ExitCode = -1,
                WallMs = clock.ElapsedMilliseconds,
                LogPath = logPath,
                FirstError = "failed to launch: " + ex.Message,
            };
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.StandardInput.Close();

        bool timedOut = false;
        using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown))
        {
            timeout.CancelAfter(_config.JobTimeout);
            try
            {
                await proc.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                // The in-game watchdog (sim_stall_frames) only fires while the loop is still ticking.
                // A process wedged before that - or during Godot's own boot - can only be ended from
                // out here, and a hung job would otherwise hold its worker slot for the whole batch.
                timedOut = !shutdown.IsCancellationRequested;
                TryKill(proc);
                await proc.WaitForExitAsync(CancellationToken.None);
            }
        }

        // Flushes the async output handlers. Without it the tail of a run - including the
        // game_result line on a fast game - can still be in flight when we read the accumulator.
        proc.WaitForExit();
        clock.Stop();

        int exitCode = proc.ExitCode;
        SimOutcome outcome =
            timedOut ? SimOutcome.Timeout
            : exitCode == 0 && parsed.HasResult ? SimOutcome.Clean
            : exitCode == 2 ? SimOutcome.ResultWithErrors
            : exitCode == 3 ? SimOutcome.Stalled
            : SimOutcome.Crashed;

        return parsed.ToResult(job, outcome, exitCode, clock.ElapsedMilliseconds, logPath);
    }

    private static void TryKill(Process proc)
    {
        try
        {
            if (!proc.HasExited) proc.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { /* already gone */ }
        catch (SystemException) { /* raced with exit */ }
    }
}

/// <summary>
/// Scrapes the events the orchestrator cares about out of a run's stdout.
///
/// The consumer rule the CLI documents: Godot prints its own banner before any user code runs, so a
/// parser must ignore stdout lines that are not valid JSON. That is why every line goes through a
/// tolerant try-parse instead of being trusted.
/// </summary>
internal sealed class ResultAccumulator
{
    private string? _resultJson;
    private string? _winner, _endReason, _firstError;
    private int _axis, _allies, _finalRound, _prompts, _errors, _resumes;
    private readonly Dictionary<string, int> _factions = new();
    private readonly Dictionary<string, RoundSeries> _rounds = new();

    public bool HasResult => _resultJson != null;

    public void Feed(string line)
    {
        // Cheap gate before the parser: the overwhelming majority of noise lines (Godot's banner,
        // shader warnings, leaked-RID chatter at exit) never start with a brace.
        string trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '{') return;

        JsonElement root;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(trimmed);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return;
        }

        if (!root.TryGetProperty("type", out JsonElement typeEl)) return;

        switch (typeEl.GetString())
        {
            case "game_result":
                _resultJson = trimmed;
                _winner = Str(root, "winner");
                _endReason = Str(root, "end_reason");
                _axis = Int(root, "axis");
                _allies = Int(root, "allies");
                _finalRound = Int(root, "final_round");
                _prompts = Int(root, "prompts");
                _errors = Int(root, "errors");
                _resumes = Int(root, "resumes");
                if (root.TryGetProperty("factions", out JsonElement f) && f.ValueKind == JsonValueKind.Object)
                    foreach (JsonProperty p in f.EnumerateObject())
                        _factions[p.Name] = p.Value.TryGetInt32(out int v) ? v : 0;
                break;

            case "round_scores":
                if (root.TryGetProperty("factions", out JsonElement rounds)
                    && rounds.ValueKind == JsonValueKind.Object)
                    foreach (JsonProperty faction in rounds.EnumerateObject())
                        _rounds[faction.Name] = new RoundSeries
                        {
                            Team = Str(faction.Value, "team") ?? "UNKNOWN",
                            Deltas = IntArray(faction.Value, "deltas"),
                            Totals = IntArray(faction.Value, "totals"),
                        };
                break;

            case "game_error":
                _firstError ??= Str(root, "message");
                break;
        }
    }

    private static int[] IntArray(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out JsonElement arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<int>();

        List<int> values = new();
        foreach (JsonElement item in arr.EnumerateArray())
            values.Add(item.TryGetInt32(out int v) ? v : 0);
        return values.ToArray();
    }

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int Int(JsonElement e, string name)
        => e.TryGetProperty(name, out JsonElement v) && v.TryGetInt32(out int i) ? i : 0;

    public SimResult ToResult(SimJob job, SimOutcome outcome, int exitCode, long wallMs, string logPath) => new()
    {
        Job = job,
        Outcome = outcome,
        ExitCode = exitCode,
        WallMs = wallMs,
        LogPath = logPath,
        ResultJson = _resultJson,
        Winner = _winner,
        AxisTotal = _axis,
        AlliesTotal = _allies,
        FinalRound = _finalRound,
        EndReason = _endReason,
        Prompts = _prompts,
        Errors = _errors,
        Resumes = _resumes,
        Factions = _factions,
        FirstError = _firstError,
        Rounds = _rounds,
    };
}
