namespace QGSim;

/// <summary>
/// Everything a batch needs, parsed from the command line. Also owns the job matrix: the seed and
/// decision-seed ranges cross-multiply into the work queue.
/// </summary>
public sealed class SimConfig
{
    public string GodotPath { get; private set; } = DefaultGodotPath();
    public string ProjectPath { get; private set; } = DefaultProjectPath();
    public string OutputDirectory { get; private set; } = "";
    public string LogDirectory => Path.Combine(OutputDirectory, "logs");

    public List<string> Scenarios { get; } = new();
    public List<int> Seeds { get; } = new();
    public List<int> DecisionSeeds { get; } = new();

    public double BotPass { get; private set; }
    public double BotDiscard { get; private set; }

    /// <summary>
    /// Chance the bot considers a card the board has made pointless. 0 (the default) keeps hollow plays
    /// out of a balance sample; 1 restores uniform play, which is the wider net for a fuzzing sweep.
    /// See RandomInputProvider._hollowChance.
    /// </summary>
    public double BotHollow { get; private set; }

    public int Workers { get; private set; }
    public TimeSpan JobTimeout { get; private set; } = TimeSpan.FromMinutes(5);

    /// <summary>Keep the raw stdout of clean runs too, not just of failures.</summary>
    public bool KeepAllLogs { get; private set; }

    public bool SkipBuild { get; private set; }

    /// <summary>
    /// Default worker count: half the cores.
    ///
    /// NOT the core count. Every worker already fans out internally - GameStateCalculator.CalculateAll
    /// runs a Parallel.ForEach over six factions roughly a thousand times per game - so one worker is
    /// already several cores busy, and past a point more workers only trade cache for context switches.
    ///
    /// Measured on a 16-core machine, 16 games of Scenario_Basic:
    ///   j=2 22.9s | j=4 13.6s | j=6 10.4s | j=8 9.2s | j=12 9.2s | j=16 9.0s
    /// The curve is flat from 8 onward, so half the cores buys all of the available speedup without
    /// leaving the machine unusable while a sweep runs. Override with --workers.
    /// </summary>
    public static int DefaultWorkers() => Math.Max(1, Environment.ProcessorCount / 2);

    private static string DefaultGodotPath()
    {
        string? fromEnv = Environment.GetEnvironmentVariable("QG_GODOT");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

        // The _console build specifically: the plain Windows binary detaches from the console and
        // writes nothing to stdout, which would leave every job looking like a crash.
        return @"C:\Users\Huggles\Desktop\Godot\Godot_v4.7.1-stable_mono_win64_console.exe";
    }

    /// <summary>Walks up from the executable to find the Godot project - this app lives in sim/ under it.</summary>
    private static string DefaultProjectPath()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "project.godot"))) return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    public IReadOnlyList<SimJob> BuildJobs()
    {
        List<SimJob> jobs = new();
        foreach (string scenario in Scenarios)
            foreach (int seed in Seeds)
                foreach (int decisionSeed in DecisionSeeds)
                    jobs.Add(new SimJob(scenario, seed, decisionSeed, BotPass, BotDiscard, BotHollow));
        return jobs;
    }

    /// <summary>
    /// Parses argv. Returns null and prints why when the arguments do not describe a runnable batch,
    /// so a typo in a range fails here rather than after Godot has already been spawned a hundred times.
    /// </summary>
    public static SimConfig? Parse(string[] args)
    {
        SimConfig c = new();
        int? workers = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string Next(string name) => i + 1 < args.Length
                ? args[++i]
                : throw new ArgumentException($"{name} needs a value");

            try
            {
                switch (arg)
                {
                    case "--godot": c.GodotPath = Next(arg); break;
                    case "--project": c.ProjectPath = Next(arg); break;
                    case "--out": c.OutputDirectory = Next(arg); break;
                    case "--scenario": c.Scenarios.AddRange(SplitList(Next(arg))); break;
                    case "--seeds": c.Seeds.AddRange(ParseRange(Next(arg))); break;
                    case "--decision-seeds": c.DecisionSeeds.AddRange(ParseRange(Next(arg))); break;
                    case "--bot-pass": c.BotPass = ParseProbability(Next(arg), arg); break;
                    case "--bot-discard": c.BotDiscard = ParseProbability(Next(arg), arg); break;
                    case "--bot-hollow": c.BotHollow = ParseProbability(Next(arg), arg); break;
                    case "--workers" or "-j": workers = int.Parse(Next(arg)); break;
                    case "--timeout": c.JobTimeout = TimeSpan.FromSeconds(double.Parse(Next(arg),
                        System.Globalization.CultureInfo.InvariantCulture)); break;
                    case "--keep-logs": c.KeepAllLogs = true; break;
                    case "--skip-build": c.SkipBuild = true; break;
                    default:
                        Console.Error.WriteLine($"unknown argument: {arg}");
                        return null;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
            {
                Console.Error.WriteLine($"bad value for {arg}: {ex.Message}");
                return null;
            }
        }

        if (c.Scenarios.Count == 0) c.Scenarios.Add("Scenario_Basic");
        if (c.Seeds.Count == 0) c.Seeds.Add(42);
        if (c.DecisionSeeds.Count == 0) c.DecisionSeeds.AddRange(Enumerable.Range(1, 10));

        c.Workers = workers ?? DefaultWorkers();
        if (c.Workers < 1)
        {
            Console.Error.WriteLine("--workers must be at least 1");
            return null;
        }

        if (string.IsNullOrEmpty(c.OutputDirectory))
            c.OutputDirectory = Path.Combine(c.ProjectPath, "sim", "runs",
                DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        if (!File.Exists(c.GodotPath))
        {
            Console.Error.WriteLine($"Godot binary not found: {c.GodotPath}");
            Console.Error.WriteLine("Set --godot or the QG_GODOT environment variable.");
            return null;
        }

        if (!File.Exists(Path.Combine(c.ProjectPath, "project.godot")))
        {
            Console.Error.WriteLine($"no project.godot under: {c.ProjectPath}");
            return null;
        }

        return c;
    }

    private static IEnumerable<string> SplitList(string value)
        => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static double ParseProbability(string value, string name)
    {
        double parsed = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        if (parsed is < 0 or > 1) throw new ArgumentException($"{name} must be between 0 and 1");
        return parsed;
    }

    /// <summary>
    /// Accepts <c>7</c>, <c>1-100</c>, and comma-joined mixtures of both, so a batch can be a sweep,
    /// a handful of interesting seeds, or a single reproduction.
    /// </summary>
    public static IEnumerable<int> ParseRange(string value)
    {
        List<int> outp = new();
        foreach (string part in SplitList(value))
        {
            int dash = part.IndexOf('-', 1);
            if (dash > 0)
            {
                int lo = int.Parse(part[..dash]);
                int hi = int.Parse(part[(dash + 1)..]);
                if (hi < lo) throw new ArgumentException($"empty range: {part}");
                for (int v = lo; v <= hi; v++) outp.Add(v);
            }
            else
            {
                outp.Add(int.Parse(part));
            }
        }
        if (outp.Count == 0) throw new ArgumentException("no values");
        return outp;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            qgsim - run QG headless sim games in parallel and aggregate the results

            USAGE
              qgsim [options]

            OPTIONS
              --scenario NAME[,NAME]   scenarios to run          (default Scenario_Basic)
              --seeds RANGE            board/deck seeds          (default 42)
              --decision-seeds RANGE   bot seeds                 (default 1-10)
              --bot-pass 0..1          chance to pass a prompt   (default 0)
              --bot-discard 0..1       chance to take the optional end-of-turn discard (default 0)
              --bot-hollow 0..1        chance to play a card the board has made pointless (default 0)
              -j, --workers N          concurrent Godot processes (default cores/2)
              --timeout SECONDS        hard kill per job         (default 300)
              --out DIR                results directory         (default sim/runs/<timestamp>)
              --godot PATH             Godot binary              (or set QG_GODOT)
              --project PATH           Godot project root        (default: found by walking up)
              --keep-logs              keep stdout of clean runs too, not just failures
              --skip-build             do not run dotnet build first

            RANGE is `5`, `1-100`, or a comma-joined mix: `1-50,77,90-99`.

            EXAMPLES
              qgsim --decision-seeds 1-200 -j 4
              qgsim --scenario Scenario_Basic,Scenario_OneRound --seeds 42,1,7 --decision-seeds 1-50
              qgsim --seeds 42 --decision-seeds 7 --keep-logs      # reproduce one game

            EXIT CODES
              0  every run clean
              1  at least one run had rules errors, stalled, timed out, or crashed
              2  the batch itself could not run (bad arguments, build failure)
            """);
    }
}
