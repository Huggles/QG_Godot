using System.Threading.Tasks;

/// <summary>
/// Host-only gate that freezes the game while a player cannot be reached.
///
/// Awaited at the points every bit of host progress passes through — a ChangeEvent or PresentationEvent
/// being applied, an input request going out — so the game stops cleanly BETWEEN events rather than
/// halfway through one. Clients need no gate of their own: with nothing broadcast, they simply wait.
/// </summary>
public static class GamePause
{
    private static TaskCompletionSource<bool> _running = NewRunning(completed: true);

    public static bool IsPaused => !_running.Task.IsCompleted;

    /// <summary>Completes at once while running; otherwise when <see cref="Resume"/> is called.</summary>
    public static Task WhenRunning() => _running.Task;

    /// <summary>
    /// Waits out a pause, then unwinds (benignly) if the session was left or the loop recovered while
    /// parked — the same checks the caller ran before it got here.
    /// </summary>
    public static async Task Gate(int? capturedEpoch = null)
    {
        if (!IsPaused) return;
        int generation = ErrorReporter.SessionGeneration;
        await WhenRunning();
        ErrorReporter.ThrowIfSessionAbandoned(generation);
        if (capturedEpoch is int epoch) ErrorReporter.ThrowIfStaleEpoch(epoch);
    }

    public static void Pause()
    {
        if (IsPaused) return;
        _running = NewRunning(completed: false);
        DebugUtilities.PrintPeer("GamePause: game paused");
    }

    public static void Resume()
    {
        if (!IsPaused) return;
        _running.TrySetResult(true);
        DebugUtilities.PrintPeer("GamePause: game resumed");
    }

    /// <summary>
    /// Session teardown. Releases anything parked on the gate rather than leaving it dangling: the session
    /// has already been abandoned by then, so each released chain unwinds on its own generation check.
    /// </summary>
    public static void Reset() => Resume();

    private static TaskCompletionSource<bool> NewRunning(bool completed)
    {
        TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (completed) tcs.SetResult(true);
        return tcs;
    }
}
