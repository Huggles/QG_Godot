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

        // Menu music is a property of not being in the game, so it is decided here rather than in
        // each screen: everything that is not Game.tscn is a menu screen and keeps the same track
        // running (PlayMusic is a no-op for the track already playing, so navigating between screens
        // does not restart it), and entering the game silences it. Coming back out — to the victory
        // screen or the main menu — starts it again through this same call, which also ends whatever
        // in-game playlist was running.
        //
        // Only the silencing half of the game case belongs here: the in-game playlist depends on
        // which factions this peer ended up with, so it is started later, from
        // MultiplayerSession.StartSession, once the player-faction registry is populated.
        if (scenePath == GameScenePath)
        {
            AudioManager.StopMusic();
        }
        else
        {
            AudioManager.PlayMusic(AudioManager.MenuMusicTrack);
        }

        if (leaveSession)
        {
            // FIRST, above every release below, for the reason ErrorReporter.RequestResume documents at its
            // own bump: anything this teardown releases must unwind as belonging to a dead session
            // rather than carry on. CancelPendingAwaiters alone is not enough — it cancels the request
            // in flight, and a loop that catches that as a skip simply issues the next one.
            ErrorReporter.AbandonSession();

            // Close before nulling. ENetMultiplayerPeer is RefCounted, so assigning null only drops the
            // engine's reference and the socket is released whenever the last one happens to go — which
            // leaves port 7777 bound and makes the next CreateServer fail with a bare "Failed to host".
            // Latent on the existing Quit-then-Host path; loading a save makes that round trip routine.
            if (from.Multiplayer?.MultiplayerPeer is { } peer)
            {
                peer.Close();
                from.Multiplayer.MultiplayerPeer = null;
            }

            // Back to the transport's own answer for "who am I"; the next session's host will say.
            SessionIdentity.Reset();

            // ChatService is an autoload, so without this one game's conversation opens the next.
            ChatService.Instance?.Clear();

            // NetworkApi is an autoload, so its pending input requests outlive the game scene. Quitting
            // while a prompt is open otherwise leaves a live awaiter holding a multi-minute backstop
            // timer, which later resolves a TaskCompletionSource belonging to a dead loop and tries to
            // abort input at peers that no longer exist. Save-then-quit-then-load makes this routine.
            ErrorReporter.CancelPendingAwaiters();

            // Client-local reaction-skip armings. UNTIL_ACTIVATABLE never expires, so without this an
            // arming survives into the next game and silently auto-passes empty reaction windows for a
            // faction the player may no longer control.
            ReactionSkipPreference.ClearAll();

            // A tutorial's input provider is a static that nothing else clears, so without this the
            // NEXT game's prompts would be answered from a script that ended with the last one.
            // Reset() only drops the override when a tutorial actually installed it — a CLI session
            // owns the same seam for the life of its process and must not be disturbed.
            TutorialRuntime.Reset();
            GameManager.PendingTutorialPath = null;

            // Same seam, same rule: the AI seats' provider is a static, and each seat's bot holds a
            // decision stream that must not follow a player into their next game. Reset() only drops
            // the override when AiSeatRuntime installed it.
            AiSeatRuntime.Reset();

            // A restore abandoned half way must not leave the next game silent and instant.
            ReplayContext.Reset();

            // Otherwise leaving a restored game and starting a fresh one restores it all over again.
            GameManager.PendingSave = null;
            GameManager.PendingScenarioJson = null;

            // Idempotent and null-safe. Without it a restore that fails after the cover is up leaves the
            // player looking at a permanent black screen with no way out.
            GameManager.Instance?.HideLoadingScreen();

            // A barrier that never came up in the session being left still holds parked ready reports.
            // They are keyed by a node path that the next game reuses verbatim, so leaving them would
            // pre-satisfy that game's barrier with peers who are not there.
            NetworkApi.ClearBufferedBarrierReports();

            // Likewise the "Waiting on …" list: prompts left open by the session being abandoned never
            // announce their closure, and a stale name would carry into the next game's label.
            InputRequest.ClearAwaitingInput();

            // Quitting mid play step leaves the round as CardPlayRound.Current. The next game's
            // StartGame then routes its draws into it, its stale epoch throws AbortedEpochException
            // (benign, so unlogged), and the new game stalls before the opening deal.
            CardPlayPool.ClearPool();

            // A Steam-hosted session rides on a Steam lobby, so dropping the peer without releasing
            // the lobby would leave a stale entry in friends' lists and block the next host attempt
            // (the peer refuses to host on a lobby it does not own outright). Harmless no-op when
            // there is no lobby, which is every ENet session.
            SteamworksApi.Instance?.LeaveCurrentLobby();

            // Both are keyed by peer id and hold Godot wrappers from the scene being freed, and both
            // outlive it: the registry is only ever cleared at the START of the next game
            // (NetworkApi.LoadPlayers), so between games it answers questions about the last one.
            PlayerFactionRegistry.Clear();
            PlayerScene.ClearCurrent();

            // Last, and deferred: NetworkApi parents the session to itself (an autoload), so nothing
            // ever freed it and each game left a live MultiplayerSession, GameFlow and synchronizer
            // behind to replicate at the next lobby's peers. After AbandonSession above, so the
            // subtree is already inert when it goes.
            if (GodotObject.IsInstanceValid(MultiplayerSession.Instance))
                MultiplayerSession.Instance.QueueFree();
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
