using System.Diagnostics;

namespace QGSim;

/// <summary>
/// Entry point. Builds the job matrix, runs it across a bounded pool of headless Godot processes,
/// and aggregates what comes back.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            SimConfig.PrintUsage();
            return 0;
        }

        SimConfig? config = SimConfig.Parse(args);
        if (config == null) return 2;

        IReadOnlyList<SimJob> jobs = config.BuildJobs();
        if (jobs.Count == 0)
        {
            Console.Error.WriteLine("no jobs to run");
            return 2;
        }

        Directory.CreateDirectory(config.LogDirectory);

        if (!config.SkipBuild && !BuildGame(config)) return 2;

        Console.WriteLine($"Running {jobs.Count} job(s) across {config.Workers} worker(s).");
        Console.WriteLine($"Output: {config.OutputDirectory}");
        Console.WriteLine();

        // Ctrl+C stops scheduling new jobs and kills the ones in flight, then still writes whatever
        // completed. A half-finished sweep is worth keeping - the jobs that did complete are as valid
        // as they would have been in a batch that ran to the end.
        using CancellationTokenSource shutdown = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            if (!shutdown.IsCancellationRequested)
            {
                Console.WriteLine("\nInterrupted - stopping workers, results so far will still be written.");
                shutdown.Cancel();
            }
        };

        Stopwatch clock = Stopwatch.StartNew();
        IReadOnlyList<SimResult> results = await RunAllAsync(config, jobs, shutdown.Token);
        clock.Stop();

        if (results.Count == 0)
        {
            Console.Error.WriteLine("no runs completed");
            return 2;
        }

        PruneLogs(config, results);
        Aggregator.Write(config, results, clock.Elapsed);

        // The sorted results.jsonl now holds everything the partial did, so the safety net has served
        // its purpose. Left behind it would be a second, worse copy for someone to pick up by mistake.
        try { File.Delete(Path.Combine(config.OutputDirectory, "results.partial.jsonl")); }
        catch (IOException) { /* not worth failing a finished batch over */ }

        return results.Any(Aggregator.NeedsAttention) ? 1 : 0;
    }

    /// <summary>
    /// The worker pool: a fixed number of consumers pulling from one shared queue.
    ///
    /// A queue rather than a partition of the job list, because run times vary by several-fold with
    /// the decision seed - a game that ends early is much cheaper than one that goes the distance.
    /// Pre-slicing the work would leave workers idle behind whichever slice happened to draw the long
    /// games; pulling on demand keeps every worker busy until the queue is actually empty.
    /// </summary>
    private static async Task<IReadOnlyList<SimResult>> RunAllAsync(
        SimConfig config, IReadOnlyList<SimJob> jobs, CancellationToken shutdown)
    {
        JobRunner runner = new(config);
        Queue<SimJob> queue = new(jobs);
        List<SimResult> results = new();
        int completed = 0;

        object gate = new();

        // Crash insurance. Everything else is written once, at the end, from memory - which is fine
        // for a 60-game batch and not fine for a 6000-game one that runs for an hour: an orchestrator
        // that dies at game 5500 would otherwise have nothing to show for any of it. Ctrl+C is already
        // handled gracefully; this covers the ways a process ends without asking.
        //
        // Unsorted, because it is written in completion order and concurrency makes that arbitrary.
        // That is exactly why it is a separate file: results.jsonl is sorted so it can be diffed
        // between batches, and half-sorting it here would quietly cost that. Main deletes this once
        // the real output is safely written.
        string partialPath = Path.Combine(config.OutputDirectory, "results.partial.jsonl");
        await using StreamWriter partial = new(partialPath, append: false) { AutoFlush = true };

        async Task Worker()
        {
            while (true)
            {
                SimJob job;
                lock (gate)
                {
                    if (shutdown.IsCancellationRequested || queue.Count == 0) return;
                    job = queue.Dequeue();
                }

                SimResult result;
                try
                {
                    result = await runner.RunAsync(job, shutdown);
                }
                catch (Exception ex)
                {
                    // One job blowing up must not take the batch with it - the other several hundred
                    // runs are still worth having.
                    result = new SimResult
                    {
                        Job = job,
                        Outcome = SimOutcome.Crashed,
                        ExitCode = -1,
                        WallMs = 0,
                        LogPath = Path.Combine(config.LogDirectory, job.Id + ".jsonl"),
                        FirstError = ex.Message,
                    };
                }

                lock (gate)
                {
                    results.Add(result);
                    if (result.ResultJson != null) partial.WriteLine(result.ResultJson);
                    Report(result, ++completed, jobs.Count);
                }
            }
        }

        await Task.WhenAll(Enumerable.Range(0, config.Workers).Select(_ => Worker()));
        return results;
    }

    /// <summary>One line per finished job. Called under the lock, so lines never interleave.</summary>
    private static void Report(SimResult r, int completed, int total)
    {
        string detail = r.HasResult
            ? $"{r.Winner} {r.AxisTotal}-{r.AlliesTotal} r{r.FinalRound} [{r.Prompts} prompts]"
            : r.FirstError is { } e ? Shorten(e) : "no result";

        ConsoleColor previous = Console.ForegroundColor;
        Console.ForegroundColor = r.Outcome switch
        {
            SimOutcome.Clean => previous,
            SimOutcome.ResultWithErrors => ConsoleColor.Yellow,
            // Not red: the game finished and its result counts. Dimmed only so a batch scrolling past
            // does not read it as a run that needs chasing.
            SimOutcome.ResultThenCrash => ConsoleColor.DarkGray,
            _ => ConsoleColor.Red,
        };

        Console.WriteLine($"[{completed,4}/{total}] {Aggregator.Label(r.Outcome),-14} "
                          + $"{r.Job.Id,-34} {r.WallMs / 1000.0,6:F1}s  {detail}");
        Console.ForegroundColor = previous;
    }

    private static string Shorten(string s)
    {
        s = s.Replace('\n', ' ').Replace('\r', ' ');
        return s.Length <= 70 ? s : s[..70] + "...";
    }

    /// <summary>
    /// Delete the stdout of clean runs unless asked to keep them. A clean sim run's log is only a
    /// handful of lines, but a sweep is thousands of files and the interesting ones are the failures -
    /// which are always kept, so nothing needed for a repro is thrown away.
    /// </summary>
    private static void PruneLogs(SimConfig config, IReadOnlyList<SimResult> results)
    {
        if (config.KeepAllLogs) return;
        foreach (SimResult r in results.Where(r => r.Outcome == SimOutcome.Clean))
        {
            try { File.Delete(r.LogPath); }
            catch (IOException) { /* held open, or already gone - not worth failing the batch */ }
        }
    }

    /// <summary>
    /// Build the game assembly once, up front.
    ///
    /// Skipping it would let every worker discover a stale or broken build simultaneously, and a
    /// compile error would then read as several hundred crashed runs instead of one clear message.
    /// </summary>
    private static bool BuildGame(SimConfig config)
    {
        Console.WriteLine("Building the game assembly...");

        ProcessStartInfo info = new()
        {
            FileName = "dotnet",
            WorkingDirectory = config.ProjectPath,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("build");
        info.ArgumentList.Add(Path.Combine(config.ProjectPath, "Quartermaster General.csproj"));
        info.ArgumentList.Add("-v");
        info.ArgumentList.Add("q");
        info.ArgumentList.Add("--nologo");

        try
        {
            using Process? proc = Process.Start(info);
            if (proc == null)
            {
                Console.Error.WriteLine("could not start dotnet build");
                return false;
            }
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                Console.Error.WriteLine($"build failed (exit {proc.ExitCode}) - not running any jobs");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"could not run dotnet build: {ex.Message}");
            return false;
        }

        Console.WriteLine("Build OK.");
        Console.WriteLine();
        return true;
    }
}
