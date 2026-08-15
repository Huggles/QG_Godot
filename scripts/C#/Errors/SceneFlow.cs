using Godot;

/// <summary>
/// Single place scene transitions go through, so <see cref="ErrorReporter.IsShuttingDown"/> is set
/// before the outgoing scene is freed.
///
/// Teardown legitimately produces ObjectDisposedException and NullReferenceException from freed
/// Godot wrappers (an in-flight animation touching a unit node that just went away, a queue draining
/// against a disposed GameState). Those are real bugs during play, so they must not be
/// blanket-whitelisted — but during a scene change they are noise, and popping a modal over a screen
/// that is being replaced is worse than useless. Gating on the flag distinguishes the two.
/// </summary>
public static class SceneFlow
{
    /// <summary>The in-game scene. A constant because <see cref="ChangeScene"/> keys the loading
    /// screen off it — a drifting literal at a call site would silently skip the cover.</summary>
    public const string GameScenePath = "res://scenes/Game.tscn";

    /// <summary>
    /// Change to <paramref name="scenePath"/>, deferred so it is safe to call from a signal handler
    /// or button callback.
    /// </summary>
    /// <param name="leaveSession">
    /// Null the multiplayer peer first, so a fresh game can be hosted or joined afterwards. Use when
    /// leaving an active session (as opposed to moving between menu screens).
    /// </param>
    public static void ChangeScene(Node from, string scenePath, bool leaveSession = false)
    {
        ErrorReporter.IsShuttingDown = true;

        if (leaveSession)
        {
            if (from.Multiplayer?.MultiplayerPeer != null)
                from.Multiplayer.MultiplayerPeer = null;

            // A barrier that never came up in the session being left still holds parked ready reports.
            // They are keyed by a node path that the next game reuses verbatim, so leaving them would
            // pre-satisfy that game's barrier with peers who are not there.
            NetworkApi.ClearBufferedBarrierReports();

            // A Steam-hosted session rides on a Steam lobby, so dropping the peer without releasing
            // the lobby would leave a stale entry in friends' lists and block the next host attempt
            // (the peer refuses to host on a lobby it does not own outright). Harmless no-op when
            // there is no lobby, which is every ENet session.
            SteamworksApi.Instance?.LeaveCurrentLobby();
        }

        SceneTree tree = from.GetTree();

        // Entering the game: raise the cover and let it actually be PRESENTED before handing over.
        // Adding the overlay is not enough on its own — ChangeSceneToFile loads Game.tscn
        // synchronously and Godot keeps showing the last drawn frame while it does, so a cover that
        // was added but never drawn leaves the player staring at the old menu for the whole load and
        // then flashing the cover once the game is already up. Waiting for one FramePostDraw is what
        // makes the cover span the load instead of trailing it. Skipped headless: there is nothing to
        // present, and the dummy renderer must never be relied on to tick this signal.
        if (scenePath == GameScenePath && !GameContext.IsHeadless && GameManager.Instance != null)
        {
            GameManager gameManager = GameManager.Instance; // outlives the scene change; `from` does not
            gameManager.ShowLoadingScreen();

            Guard.FireAndForget(async () =>
            {
                await gameManager.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                // Still deferred, so the blocking load runs in normal idle processing rather than
                // inside the render callback we just woke up on.
                tree.CallDeferred(SceneTree.MethodName.ChangeSceneToFile, scenePath);
            }, "SceneFlow.ChangeScene");
            return;
        }

        tree.CallDeferred(SceneTree.MethodName.ChangeSceneToFile, scenePath);
    }

    /// <summary>
    /// Clear the shutting-down flag. Called once the new scene is up, so failures in it are reported
    /// normally again.
    /// </summary>
    public static void SceneReady() => ErrorReporter.IsShuttingDown = false;
}
