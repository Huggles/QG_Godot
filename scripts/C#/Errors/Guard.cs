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
    /// <param name="stallsLoop">
    /// True at the call sites that DRIVE the turn loop (GameFlow's step dispatch and the step
    /// handlers). A failure escaping one of those means the step's Finished event never fired, so the
    /// loop is stopped and the popup must offer Continue. Leave false everywhere else: a queue or
    /// animation failure is caught locally and stalls nothing, and offering Continue for it would
    /// advance a turn step nothing was waiting on.
    /// </param>
    public static void FireAndForget(Func<Task> work, string context, Faction? target = null,
                                     bool stallsLoop = false)
    {
        _ = Observe(work, context, target, stallsLoop);
    }

    /// <summary>Await <paramref name="work"/>, reporting instead of propagating any failure.</summary>
    public static async Task Run(Func<Task> work, string context, Faction? target = null,
                                 bool stallsLoop = false)
    {
        await Observe(work, context, target, stallsLoop);
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

    private static async Task Observe(Func<Task> work, string context, Faction? target, bool stallsLoop)
    {
        try
        {
            if (work == null) return;
            await work();
        }
        catch (Exception e)
        {
            if (stallsLoop)
                ErrorReporter.ReportLoopStalled(e, context, target);
            else
                ErrorReporter.Report(e, context, target);
        }
    }
}
