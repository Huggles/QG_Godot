using System.Threading.Tasks;

/// <summary>
/// Flags for the two things a save-game restore has to do differently from live play.
///
/// Deliberately two flags rather than one, because they answer different questions and live on
/// different peers:
///
///   <see cref="IsReplaying"/>      — "are these events being RE-applied from a save?" Host-only.
///                                    Read by the handful of ExecuteAsync branches that normally let
///                                    the host decide an outcome the client is merely told (see
///                                    RecycleCardChangeEvent): during a replay the host must behave
///                                    like a client and use the recorded outcome instead.
///   <see cref="IsFastForwarding"/> — "should presentation and pacing be skipped?" Host AND client.
///                                    A client never replays anything; it just drains the host's
///                                    broadcast burst, and it must do so without animating.
///
/// IsFastForwarding is a superset: a host replaying is always also fast-forwarding.
/// </summary>
public static class ReplayContext
{
    /// <summary>
    /// Host only: ChangeEvents are being re-applied from a save rather than produced by play.
    /// Set for the duration of <c>MultiplayerSession.RestoreSavedGame</c>'s replay loop.
    /// </summary>
    public static bool IsReplaying { get; set; }

    /// <summary>
    /// Host or client: suppress every animation, modal, sound and pacing delay. On the host this is
    /// on for the replay; on a client it is switched on by an Rpc before the burst arrives and off
    /// again once the loading cover lifts.
    /// </summary>
    public static bool IsFastForwarding { get; private set; }

    public static void BeginFastForward() => IsFastForwarding = true;

    /// <summary>Idempotent: called from a finally that may run on a peer that never fast-forwarded.</summary>
    public static void EndFastForward() => IsFastForwarding = false;

    /// <summary>
    /// Reset both flags. Called when a session is torn down, so a restore abandoned half way cannot
    /// leave the next game silent and instant.
    /// </summary>
    public static void Reset()
    {
        IsReplaying = false;
        IsFastForwarding = false;
    }

    /// <summary>
    /// Stand-in for a hardcoded <c>Task.Delay</c> on the logic path. Pacing that goes through
    /// <see cref="GameSettings"/> is already handled by GetDuration returning 0; this is for the
    /// handful of literal millisecond values that do not.
    /// </summary>
    public static Task Pace(int milliseconds)
        => IsFastForwarding || GameContext.IsHeadless ? Task.CompletedTask : Task.Delay(milliseconds);
}
