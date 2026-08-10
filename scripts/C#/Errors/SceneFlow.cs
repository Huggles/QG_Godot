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

        // Raise the cover here, before the outgoing scene is freed, so the transition itself and the
        // whole game build behind it are covered. No-op headless, so the CLI path is unaffected.
        if (scenePath == GameScenePath)
            GameManager.Instance?.ShowLoadingScreen();

        if (leaveSession && from.Multiplayer?.MultiplayerPeer != null)
            from.Multiplayer.MultiplayerPeer = null;

        from.GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, scenePath);
    }

    /// <summary>
    /// Clear the shutting-down flag. Called once the new scene is up, so failures in it are reported
    /// normally again.
    /// </summary>
    public static void SceneReady() => ErrorReporter.IsShuttingDown = false;
}
