using System.Text;
using System.Text.Json;

namespace QGSim;

/// <summary>
/// Turns a batch of <see cref="SimResult"/>s into the two things a run is for: a summary a human
/// reads, and files a later run can be diffed against.
/// </summary>
public static class Aggregator
{
    /// <summary>
    /// Writes results.jsonl (one game_result per line) and summary.txt.
    ///
    /// Sorted by job identity, never by completion order. game_result is deliberately free of
    /// wall-clock so that a given (seed, decision_seed) always renders the same bytes; sorting is what
    /// extends that property from one line to the whole file, which is what makes a batch usable as a
    /// regression baseline. Under concurrency, completion order is nondeterministic - writing in that
    /// order would throw the property away for no reason.
    /// </summary>
    public static void Write(SimConfig config, IReadOnlyList<SimResult> results, TimeSpan elapsed)
    {
        List<SimResult> ordered = results
            .OrderBy(r => r.Job.Scenario, StringComparer.Ordinal)
            .ThenBy(r => r.Job.Seed)
            .ThenBy(r => r.Job.DecisionSeed)
            .ToList();

        string resultsPath = Path.Combine(config.OutputDirectory, "results.jsonl");
        using (StreamWriter w = new(resultsPath, append: false))
            foreach (SimResult r in ordered.Where(r => r.HasResult))
                w.WriteLine(r.ResultJson);

        string summary = BuildSummary(config, ordered, elapsed);
        File.WriteAllText(Path.Combine(config.OutputDirectory, "summary.txt"), summary);
        Console.WriteLine();
        Console.Write(summary);

        WriteFailureIndex(config, ordered);
    }

    private static string BuildSummary(SimConfig config, List<SimResult> results, TimeSpan elapsed)
    {
        StringBuilder sb = new();
        int total = results.Count;

        sb.AppendLine("=== BATCH SUMMARY ===");
        sb.AppendLine();
        sb.AppendLine($"  scenarios      {string.Join(", ", config.Scenarios)}");
        sb.AppendLine($"  seeds          {Summarise(config.Seeds)}");
        sb.AppendLine($"  decision seeds {Summarise(config.DecisionSeeds)}");
        sb.AppendLine($"  bot_pass       {config.BotPass}   bot_discard {config.BotDiscard}");
        sb.AppendLine($"  workers        {config.Workers}");
        sb.AppendLine();

        sb.AppendLine($"  {total} run(s) in {elapsed.TotalSeconds:F1}s"
                      + (total > 0 ? $"  ({elapsed.TotalSeconds / total:F1}s per run, wall)" : ""));
        sb.AppendLine();

        foreach (SimOutcome outcome in Enum.GetValues<SimOutcome>())
        {
            int count = results.Count(r => r.Outcome == outcome);
            if (count == 0) continue;
            sb.AppendLine($"    {Label(outcome),-22} {count,5}   {Percent(count, total)}");
        }
        sb.AppendLine();

        // Only completed games can speak to balance. A stalled or timed-out run has no outcome, and
        // folding it in as a loss for whoever was behind is exactly the silent miscount the CLI's
        // separate exit code 3 exists to prevent.
        List<SimResult> scored = results.Where(r => r.HasResult).ToList();
        if (scored.Count == 0)
        {
            sb.AppendLine("  No completed games - nothing to aggregate.");
            return sb.ToString();
        }

        sb.AppendLine($"--- WIN RATE ({scored.Count} completed game(s)) ---");
        sb.AppendLine();
        foreach (IGrouping<string, SimResult> g in scored
                     .GroupBy(r => r.Winner ?? "(none)")
                     .OrderByDescending(g => g.Count()))
            sb.AppendLine($"    {g.Key,-22} {g.Count(),5}   {Percent(g.Count(), scored.Count)}");
        sb.AppendLine();

        sb.AppendLine($"    avg score      Axis {scored.Average(r => r.AxisTotal),6:F1}"
                      + $"   Allies {scored.Average(r => r.AlliesTotal),6:F1}");
        sb.AppendLine($"    avg final round      {scored.Average(r => r.FinalRound),6:F1}");
        sb.AppendLine($"    avg prompts          {scored.Average(r => r.Prompts),6:F0}");
        sb.AppendLine();

        sb.AppendLine("--- AVERAGE SCORE BY FACTION ---");
        sb.AppendLine();
        Dictionary<string, List<int>> byFaction = new();
        foreach (SimResult r in scored)
            foreach ((string faction, int score) in r.Factions)
                (byFaction.TryGetValue(faction, out List<int>? list)
                    ? list
                    : byFaction[faction] = new List<int>()).Add(score);

        foreach ((string faction, List<int> scores) in byFaction.OrderByDescending(kv => kv.Value.Average()))
            sb.AppendLine($"    {faction,-22} {scores.Average(),6:F1}   (n={scores.Count})");
        sb.AppendLine();

        sb.AppendLine("--- END REASON ---");
        sb.AppendLine();
        foreach (IGrouping<string, SimResult> g in scored
                     .GroupBy(r => r.EndReason ?? "(none)")
                     .OrderByDescending(g => g.Count()))
            sb.AppendLine($"    {g.Key,-40} {g.Count(),5}   {Percent(g.Count(), scored.Count)}");
        sb.AppendLine();

        List<SimResult> bad = results.Where(r => r.Outcome != SimOutcome.Clean).ToList();
        if (bad.Count > 0)
        {
            sb.AppendLine($"--- {bad.Count} RUN(S) NEEDING ATTENTION ---");
            sb.AppendLine();
            foreach (SimResult r in bad.Take(25))
            {
                sb.AppendLine($"    {Label(r.Outcome),-18} {r.Job.Id}");
                if (r.FirstError != null) sb.AppendLine($"        {Truncate(r.FirstError, 100)}");
                sb.AppendLine($"        reproduce: --scenario {r.Job.Scenario} "
                              + $"--seeds {r.Job.Seed} --decision-seeds {r.Job.DecisionSeed}");
            }
            if (bad.Count > 25) sb.AppendLine($"    ... and {bad.Count - 25} more (see failures.txt)");
            sb.AppendLine();
        }

        sb.AppendLine($"  results  {Path.Combine(config.OutputDirectory, "results.jsonl")}");
        sb.AppendLine($"  logs     {config.LogDirectory}");
        return sb.ToString();
    }

    /// <summary>
    /// A standalone list of every run worth re-running, with its exact reproduction arguments. Kept
    /// separate from the summary because it is the file you paste from, not the one you read.
    /// </summary>
    private static void WriteFailureIndex(SimConfig config, List<SimResult> results)
    {
        List<SimResult> bad = results.Where(r => r.Outcome != SimOutcome.Clean).ToList();
        string path = Path.Combine(config.OutputDirectory, "failures.txt");
        if (bad.Count == 0)
        {
            File.WriteAllText(path, "No failures.\n");
            return;
        }

        using StreamWriter w = new(path, append: false);
        foreach (SimResult r in bad)
        {
            w.WriteLine($"{Label(r.Outcome)}  exit {r.ExitCode}  {r.Job.Id}");
            if (r.FirstError != null) w.WriteLine($"    error: {r.FirstError}");
            if (r.Errors > 0 || r.Resumes > 0) w.WriteLine($"    errors={r.Errors} resumes={r.Resumes}");
            w.WriteLine($"    log:   {r.LogPath}");
            w.WriteLine($"    rerun: --scenario {r.Job.Scenario} --seeds {r.Job.Seed} "
                        + $"--decision-seeds {r.Job.DecisionSeed} --keep-logs");
            w.WriteLine();
        }
    }

    public static string Label(SimOutcome outcome) => outcome switch
    {
        SimOutcome.Clean => "clean",
        SimOutcome.ResultWithErrors => "result+errors",
        SimOutcome.Stalled => "stalled",
        SimOutcome.Timeout => "timeout",
        SimOutcome.Crashed => "crashed",
        _ => outcome.ToString(),
    };

    private static string Percent(int n, int total)
        => total == 0 ? "" : $"{100.0 * n / total,5:F1}%";

    private static string Summarise(List<int> values)
        => values.Count <= 6
            ? string.Join(",", values)
            : $"{values[0]}..{values[^1]} ({values.Count})";

    private static string Truncate(string s, int max)
    {
        s = s.Replace('\n', ' ').Replace('\r', ' ');
        return s.Length <= max ? s : s[..max] + "...";
    }
}
