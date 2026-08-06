using System;
using System.Threading.Tasks;

/// <summary>
/// Wraps the fire-and-forget call sites that drive the game loop.
///
/// The turn loop invokes step handlers as <c>Func&lt;Task&gt;</c> and discards the Task
/// (GameFlow's TurnStepCounter setter, <c>_ = ProcessXxxStep()</c> in the step handlers,
/// <c>_ = ProcessQueue()</c> in both queues). A discarded Task swallows its exception into an
/// unobserved Task, so a failure produced a silent permanent hang rather than anything visible.
/// Routing those sites through <see cref="FireAndForget"/> is what makes them report.
///
/// This is the PRIMARY mechanism. TaskScheduler.UnobservedTaskException (wired in ErrorReporter)
/// is only a backstop — it fires on GC finalization, so it is late and never fires at all while
/// the Task stays referenced.
/// </summary>
public static class Guard
{
    /// <summary>
    /// Start <paramref name="work"/> without awaiting it, but observe its outcome. Replaces
    /// <c>_ = SomeTask()</c>.
    /// </summary>
    /// <param name="context">Where this runs, e.g. "TurnStep SUPPLY".</param>
    /// <param name="target">Faction the work is being done for, when there is one.</param>
    public static void FireAndForget(Func<Task> work, string context, Faction? target = null)
    {
        _ = Observe(work, context, target);
    }

    /// <summary>Await <paramref name="work"/>, reporting instead of propagating any failure.</summary>
    public static async Task Run(Func<Task> work, string context, Faction? target = null)
    {
        await Observe(work, context, target);
    }

    /// <summary>
    /// Await <paramref name="work"/>, reporting any failure and returning
    /// <paramref name="fallback"/> instead of propagating.
    /// </summary>
    public static async Task<T> Run<T>(Func<Task<T>> work, string context, Faction? target = null, T fallback = default)
    {
        try
        {
            if (work == null) return fallback;
            return await work();
        }
        catch (Exception e)
        {
            ErrorReporter.Report(e, context, target);
            return fallback;
        }
    }

    /// <summary>Run a synchronous action, reporting instead of propagating.</summary>
    public static void Try(Action work, string context, Faction? target = null)
    {
        try { work?.Invoke(); }
        catch (Exception e) { ErrorReporter.Report(e, context, target); }
    }

    private static async Task Observe(Func<Task> work, string context, Faction? target)
    {
        try
        {
            if (work == null) return;
            await work();
        }
        catch (Exception e)
        {
            ErrorReporter.Report(e, context, target);
        }
    }
}
