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
        WriteRoundScores(config, ordered);
        WriteCardImpact(config, ordered);
    }

    /// <summary>
    /// Running statistics for one card, within one board seed.
    ///
    /// Sums rather than a retained sample: 2000 games x ~170 cards is 340k observations, and every
    /// figure below needs only first and second moments.
    /// </summary>
    private sealed class CardAccumulator
    {
        public string Team = "", Type = "";
        public int Games;
        public double SumX, SumY, SumXX, SumYY, SumXY;   // x = copies played, y = team-relative delta
        public int PlayedGames;
        public double PlayedDeltaSum, UnplayedDeltaSum;

        public void Add(double copiesPlayed, double delta)
        {
            Games++;
            SumX += copiesPlayed; SumY += delta;
            SumXX += copiesPlayed * copiesPlayed;
            SumYY += delta * delta;
            SumXY += copiesPlayed * delta;
            if (copiesPlayed > 0) { PlayedGames++; PlayedDeltaSum += delta; }
            else UnplayedDeltaSum += delta;
        }

        /// <summary>
        /// Pearson r between copies played and the delta. Handles multi-copy cards naturally - a deck
        /// holding four Build Army is played "at least once" in nearly every game, so a played/not
        /// split would have almost no variance to work with, while the COUNT still varies usefully.
        /// NaN when either side is constant, which is the honest answer: no variance, no correlation.
        /// </summary>
        public double Correlation()
        {
            double n = Games;
            double covariance = SumXY - SumX * SumY / n;
            double varX = SumXX - SumX * SumX / n;
            double varY = SumYY - SumY * SumY / n;
            if (varX <= 0 || varY <= 0) return double.NaN;
            return covariance / Math.Sqrt(varX * varY);
        }
    }

    /// <summary>
    /// card_impact.csv - how each card's play relates to the final score gap, per board seed.
    ///
    /// The delta is TEAM-RELATIVE: (owner's team score - opposing team score). Using the raw
    /// Axis-minus-Allies gap would rank every Axis card "good" and every Allied card "bad" purely from
    /// the Axis side's ~10-point baseline advantage, which says nothing about the card. Flipping the
    /// sign for Allied cards puts both sides on one scale, where a positive number means "the owner's
    /// team did better than usual when this card got played".
    ///
    /// Grouped per SEED, not pooled across seeds. The deal is fixed within a seed, so every game there
    /// shares one opening position and one deck order - which makes play the only thing that varies
    /// and the comparison a fair one. Pooling seeds would mix in "this board favours the Axis" and
    /// attribute it to whichever cards that board happens to hold.
    ///
    /// Correlation is NOT causation here, and the direction is genuinely ambiguous: a faction that is
    /// winning takes more turns and therefore plays more cards, so a positive r can mean the card
    /// helped OR merely that its owner was already ahead. Reading these as card strength needs the
    /// exogenous signal (which opening hand was dealt) to confirm it.
    /// </summary>
    private static void WriteCardImpact(SimConfig config, List<SimResult> results)
    {
        // seed -> "faction|card" -> stats
        Dictionary<int, Dictionary<string, CardAccumulator>> bySeed = new();

        foreach (SimResult result in results.Where(r => r.HasResult && r.Cards.Count > 0))
        {
            if (!bySeed.TryGetValue(result.Job.Seed, out var perCard))
                perCard = bySeed[result.Job.Seed] = new Dictionary<string, CardAccumulator>();

            // Copies of one card are interchangeable, so they are summed into a single count.
            Dictionary<string, (int played, string type, string faction)> perGame = new();
            foreach (CardStat card in result.Cards)
            {
                string key = card.Faction + "|" + card.Name;
                (int played, string type, string faction) prior =
                    perGame.TryGetValue(key, out var existing) ? existing : (0, card.Type, card.Faction);
                perGame[key] = (prior.played + card.Played, prior.type, prior.faction);
            }

            foreach ((string key, (int played, string type, string faction)) in perGame)
            {
                string team = result.Rounds.TryGetValue(faction, out RoundSeries? series) ? series.Team : "UNKNOWN";
                double delta = team == "AXIS"
                    ? result.AxisTotal - result.AlliesTotal
                    : result.AlliesTotal - result.AxisTotal;

                if (!perCard.TryGetValue(key, out CardAccumulator? acc))
                    acc = perCard[key] = new CardAccumulator();
                acc.Team = team;
                acc.Type = type;
                acc.Add(played, delta);
            }
        }

        string path = Path.Combine(config.OutputDirectory, "card_impact.csv");
        using StreamWriter w = new(path, append: false);
        w.WriteLine("seed,faction,card,type,team,games,avg_copies_played,pct_games_played,"
                    + "avg_delta_played,avg_delta_unplayed,impact,correlation");

        foreach (int seed in bySeed.Keys.OrderBy(s => s))
            foreach ((string key, CardAccumulator acc) in bySeed[seed]
                         .OrderByDescending(kv => Math.Abs(Impact(kv.Value))))
            {
                string[] parts = key.Split('|', 2);
                int unplayed = acc.Games - acc.PlayedGames;
                w.WriteLine($"{seed},{parts[0]},{Escape(parts[1])},{acc.Type},{acc.Team},{acc.Games},"
                            + $"{acc.SumX / acc.Games:F2},"
                            + $"{100.0 * acc.PlayedGames / acc.Games:F1},"
                            + $"{(acc.PlayedGames > 0 ? (acc.PlayedDeltaSum / acc.PlayedGames).ToString("F2") : "")},"
                            + $"{(unplayed > 0 ? (acc.UnplayedDeltaSum / unplayed).ToString("F2") : "")},"
                            + $"{(acc.PlayedGames > 0 && unplayed > 0 ? Impact(acc).ToString("F2") : "")},"
                            + $"{(double.IsNaN(acc.Correlation()) ? "" : acc.Correlation().ToString("F3"))}");
            }

        Console.WriteLine($"  cards    {path}");
    }

    /// <summary>
    /// Mean delta when the card was played minus mean delta when it was not. Zero when one side of the
    /// comparison is empty - a card played in every single game has nothing to be compared against.
    /// </summary>
    private static double Impact(CardAccumulator acc)
    {
        int unplayed = acc.Games - acc.PlayedGames;
        if (acc.PlayedGames == 0 || unplayed == 0) return 0;
        return acc.PlayedDeltaSum / acc.PlayedGames - acc.UnplayedDeltaSum / unplayed;
    }

    /// <summary>Card labels contain commas ("Guns, not Butter"), so the CSV field needs quoting.</summary>
    private static string Escape(string field)
        => field.Contains(',') || field.Contains('"')
            ? "\"" + field.Replace("\"", "\"\"") + "\""
            : field;

    /// <summary>
    /// round_scores.csv — average victory points per faction per round across the batch.
    ///
    /// The denominator is games that REACHED the round, not all games, and it is written out as its
    /// own column. Games end when a team takes a 30-point lead, so a batch mixes 11-round and
    /// 20-round games; dividing round 20 by the whole batch would report an average nobody scored,
    /// dragged toward zero by every game that had already finished. Reading avg_total at round 18
    /// therefore means "of the games still running at round 18, this was the average" — which is the
    /// honest number, and the games column is there so a thin tail is visible rather than implied.
    /// </summary>
    private static void WriteRoundScores(SimConfig config, List<SimResult> results)
    {
        // faction -> round index -> running sums
        Dictionary<string, string> teamOf = new();
        Dictionary<string, List<long>> deltaSum = new();
        Dictionary<string, List<long>> totalSum = new();
        Dictionary<string, List<int>> gameCount = new();
        int maxRounds = 0;

        foreach (SimResult result in results.Where(r => r.HasResult))
            foreach ((string faction, RoundSeries series) in result.Rounds)
            {
                teamOf[faction] = series.Team;
                List<long> deltas = Series(deltaSum, faction);
                List<long> totals = Series(totalSum, faction);
                List<int> counts = Counts(gameCount, faction);

                for (int round = 0; round < series.Totals.Length; round++)
                {
                    while (deltas.Count <= round) { deltas.Add(0); totals.Add(0); counts.Add(0); }
                    if (round < series.Deltas.Length) deltas[round] += series.Deltas[round];
                    totals[round] += series.Totals[round];
                    counts[round]++;
                }
                maxRounds = Math.Max(maxRounds, series.Totals.Length);
            }

        string path = Path.Combine(config.OutputDirectory, "round_scores.csv");
        if (maxRounds == 0)
        {
            File.WriteAllText(path, "round,faction,team,games,avg_delta,avg_total\n");
            return;
        }

        using (StreamWriter w = new(path, append: false))
        {
            w.WriteLine("round,faction,team,games,avg_delta,avg_total");
            // Faction order fixed alphabetically so two batches produce diffable files, the same
            // reason results.jsonl is sorted by job identity rather than completion order.
            foreach (string faction in teamOf.Keys.OrderBy(k => k, StringComparer.Ordinal))
                for (int round = 0; round < gameCount[faction].Count; round++)
                {
                    int games = gameCount[faction][round];
                    if (games == 0) continue;
                    w.WriteLine($"{round + 1},{faction},{teamOf[faction]},{games},"
                                + $"{(double)deltaSum[faction][round] / games:F2},"
                                + $"{(double)totalSum[faction][round] / games:F2}");
                }
        }

        AppendTeamTrajectory(config, teamOf, totalSum, gameCount, maxRounds);
        Console.WriteLine($"  rounds   {path}");
    }

    /// <summary>
    /// The team-level view, appended to summary.txt rather than the CSV: two columns over twenty rows
    /// is the thing you actually read to see when a lead opens up, and it fits on screen.
    /// </summary>
    private static void AppendTeamTrajectory(
        SimConfig config,
        Dictionary<string, string> teamOf,
        Dictionary<string, List<long>> totalSum,
        Dictionary<string, List<int>> gameCount,
        int maxRounds)
    {
        StringBuilder sb = new();
        sb.AppendLine();
        sb.AppendLine("--- AVERAGE TEAM VP BY ROUND ---");
        sb.AppendLine();
        sb.AppendLine("    round   games      AXIS    ALLIES      lead");

        for (int round = 0; round < maxRounds; round++)
        {
            double axis = 0, allies = 0;
            int games = 0;
            foreach ((string faction, string team) in teamOf)
            {
                if (round >= gameCount[faction].Count || gameCount[faction][round] == 0) continue;
                double avg = (double)totalSum[faction][round] / gameCount[faction][round];
                if (team == "AXIS") axis += avg; else allies += avg;
                games = Math.Max(games, gameCount[faction][round]);
            }
            if (games == 0) continue;

            sb.AppendLine($"    {round + 1,5}   {games,5}   {axis,7:F1}   {allies,7:F1}   {axis - allies,7:F1}");
        }

        string summaryPath = Path.Combine(config.OutputDirectory, "summary.txt");
        File.AppendAllText(summaryPath, sb.ToString());
        Console.Write(sb.ToString());
    }

    private static List<long> Series(Dictionary<string, List<long>> map, string key)
        => map.TryGetValue(key, out List<long>? list) ? list : map[key] = new List<long>();

    private static List<int> Counts(Dictionary<string, List<int>> map, string key)
        => map.TryGetValue(key, out List<int>? list) ? list : map[key] = new List<int>();

    private static string BuildSummary(SimConfig config, List<SimResult> results, TimeSpan elapsed)
    {
        StringBuilder sb = new();
        int total = results.Count;

        sb.AppendLine("=== BATCH SUMMARY ===");
        sb.AppendLine();
        sb.AppendLine($"  scenarios      {string.Join(", ", config.Scenarios)}");
        sb.AppendLine($"  seeds          {Summarise(config.Seeds)}");
        sb.AppendLine($"  decision seeds {Summarise(config.DecisionSeeds)}");
        sb.AppendLine($"  bot_pass       {config.BotPass}   bot_discard {config.BotDiscard}   bot_hollow {config.BotHollow}");
        // Its own line: unlike the three probabilities this is a long string, and it is the one field
        // that says which POLICY produced these numbers. A run directory whose summary does not name
        // its policy cannot be compared against anything later.
        sb.AppendLine($"  bot_rules      {(string.IsNullOrWhiteSpace(config.BotRules) ? "(defaults)" : config.BotRules)}");
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

        List<SimResult> bad = results.Where(NeedsAttention).ToList();
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
        List<SimResult> bad = results.Where(NeedsAttention).ToList();
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

    /// <summary>
    /// Whether a run is worth a human looking at it. ResultThenCrash is deliberately NOT: the game
    /// reached a win condition and its result is as valid as any clean run's, so listing it for
    /// re-running would send someone chasing a Godot teardown race that no seed reproduces.
    /// </summary>
    public static bool NeedsAttention(SimResult result)
        => result.Outcome != SimOutcome.Clean && result.Outcome != SimOutcome.ResultThenCrash;

    public static string Label(SimOutcome outcome) => outcome switch
    {
        SimOutcome.Clean => "clean",
        SimOutcome.ResultThenCrash => "result+teardown",
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
