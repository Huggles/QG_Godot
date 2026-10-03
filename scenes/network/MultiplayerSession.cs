using Godot;
using System;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Spawned on both host and all clients via MultiplayerSpawner (spawn_path = Data in network.tscn).
/// Acts as the sole communication API between host and clients.
/// All RPCs for game state sync go through this class.
/// Authority is always the host (peer 1).
/// </summary>
public partial class MultiplayerSession : Node
{
    public static MultiplayerSession Instance { get; private set; }
    private PeerReadinessComponent _peerReadinessComponent => GetNode<PeerReadinessComponent>("PeerReadinessComponent");
    private PeerReadinessComponent _endGameReadiness => GetNode<PeerReadinessComponent>("EndGameReadinessComponent");
    private SessionRestore _restore => GetNode<SessionRestore>("SessionRestore");

    public MultiplayerGameState GameState { get; private set; } = new();

    public override void _EnterTree()
    {
        base._EnterTree();
        Instance = this;
        DebugUtilities.PrintPeerFinest($"MultiplayerSession entered tree (IsServer={Multiplayer.IsServer()})");
    }

    public override void _Ready()
    {
        DebugUtilities.PrintPeerFinest($"MultiplayerSession ready on peer {Multiplayer.GetUniqueId()}");
        _endGameReadiness.AllPeersReady += OnAllPeersReadyForVictory; // fires on host only
        _peerReadinessComponent.RegisterReady();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (HasNode("EndGameReadinessComponent"))
            _endGameReadiness.AllPeersReady -= OnAllPeersReadyForVictory;
        if (Instance == this)
            Instance = null;
    }

    public void StartNew(string configuration)
    {
        if (!Multiplayer.IsServer()) return;

        // The host picks the seed and every peer is told it. Read here rather than in StartSession
        // because that method runs on every peer: each one would otherwise resolve its own command
        // line (and its own Environment.TickCount) and hold a different stream.
        //
        // Precedence: a CLI `seed=` argument (headless replays must reproduce regardless of menu
        // state), then whatever the menus put in GameManager.PendingSeed, then a fresh roll.
        // A save pins its own seed and nothing may override it: the log records the deck order the
        // shuffle produced, so a restore that reseeded differently would replay recorded orders on top of
        // a differently-shuffled deck and only diverge visibly several turns later. The CLI argument
        // still wins for a fresh game, which is what headless replays need.
        int seed = GameManager.PendingSave?.Seed
                   ?? CliArgs.GetInt("seed", GameManager.PendingSeed ?? System.Environment.TickCount);

        Rpc(nameof(StartSession), configuration, seed);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public async void StartSession(string configuration, int seed)
    {
        // async void: an uncaught throw here goes to the synchronization context and kills the
        // process. A failure during session init is not recoverable (the readiness barrier will
        // never complete), but it must be visible rather than a silent crash.
        try
        {
            DebugUtilities.PrintPeerFinest($"LoadGame called with config: {configuration}");

            // A session exists again from here. NetworkApi refuses to open an input request while none
            // is live, which is what stops an abandoned game's loop issuing fresh prompts into an
            // autoload that outlives it. Before any state is built, so nothing below can race it.
            ErrorReporter.BeginSession();

            // Seed before any state is built. Here rather than at boot because a second game in the
            // same process must not inherit the first game's stream. An unseeded run still records
            // the seed it picked, so any session can be re-pinned afterwards.
            //
            // The value comes from the host (see StartNew) so all peers share one stream. Deck order
            // is still replicated explicitly rather than re-derived from the seed — the shared seed
            // is a safety net, not the sync mechanism.
            GameRandom.Initialize(seed);

            // Beside the reseed, and for the same reason: once per game, before any state is built. Not
            // needed for correctness — ids only have to be unique within a session — but it keeps two
            // runs of the same seed producing comparable logs, and it is what makes SyncCounterToLatest
            // after a restore easy to reason about.
            GameMessage.ResetStream();

            // The other process-static id counter, and this one is load-bearing: unit ids are the
            // handles a save's event log uses, so the pool the game mode is about to build must be
            // numbered from zero again or a restore cannot resolve them. See UnitPool.ResetIdStream.
            UnitPool.ResetIdStream();
            DebugUtilities.PrintPeer($"RNG seed: {GameRandom.Seed}");

            // Consumed here rather than read repeatedly, so a failure part-way through cannot leave the
            // next game trying to restore this save again. Host-only: PendingSave is never set on a
            // client, which rebuilds a restored game from the log the host sends (ReceiveRestoreLog).
            SaveGame restore = Multiplayer.IsServer() ? GameManager.PendingSave : null;
            GameManager.PendingSave = null;

            GameModeMultiplayerDefault gameMode = new GameModeMultiplayerDefault();
            await gameMode.Init();
            DebugUtilities.PrintPeerFinest("Game mode initialization complete, emitting MultiplayerSessionReady");

            // The HUD goes up underneath the loading cover BEFORE any game logic runs, on every peer. A
            // restore streams its whole event log in a couple of frames, and a fresh game's StartGame
            // raises the opening discard from inside this call — either way a HUD built afterwards misses
            // the signals that populate it, and that prompt reaches a null PlayerActionLabel.Instance.
            // Null on a dedicated/headless server (it controls no faction, so has no local PlayerScene).
            PlayerScene.Current?.EnsureUiLoaded();

            if(Multiplayer.IsServer())
            {
                // Above the restore branch, not inside its else like the tutorial below: a loaded save
                // has AI seats exactly as a fresh game does, so installing there would leave a restored
                // game with its bots silently absent. A no-op unless this session has AI seats.
                AiSeatRuntime.InstallIfRequested();

                if (restore != null)
                {
                    await _restore.RestoreAsync(gameMode, restore);
                }
                else
                {
                    await gameMode.InitStartingState();

                    // After the state is built (the script may name cards the scenario dealt) and
                    // before the loop starts. A no-op unless the scenario armed a tutorial.
                    TutorialRuntime.InstallIfRequested();

                    GameFlow.Instance.StartGame();
                }
            }


            EventBus.Emit(EventBus.SignalName.GameSessionStarted);

            // In-game music. Here rather than in SceneFlow because the opening track is chosen from
            // the factions this peer controls, and the registry only becomes valid once LoadPlayers
            // has run — which it has by the time this RPC arrives. The loading cover is still up, so
            // the track starts underneath it. A no-op on a headless peer, which has no audio at all.
            GameMusic.StartForLocalPlayer();

            // Drop the cover on every peer, each once its own queue has caught up.
            if (Multiplayer.IsServer())
            {
                Rpc(nameof(HideLoadingScreenWhenReady), restore != null);

                // Only once every client has applied the restore log, so the prompt the save was taken
                // on never opens over a client still replaying. Last, because it restarts the turn loop.
                if (restore != null)
                {
                    await _restore.WhenAllPeersRestored();
                    GameFlow.Instance.ResumeAfterLoad();
                }
            }
        }
        catch (Exception e)
        {
            ErrorReporter.Report(e, "MultiplayerSession.StartSession");
        }
    }

    /// <summary>
    /// Fades out this peer's loading cover, but only once its ChangeEventQueue has drained, so the
    /// setup event burst is fully applied before the board becomes visible. Broadcast by the host at
    /// the end of session start. A bare Rpc is safe here precisely because it awaits the drain rather
    /// than acting in the receiving frame — the same shape as NetworkApi.ReceiveInputRequest.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public async void HideLoadingScreenWhenReady(bool afterRestore = false)
    {
        try
        {
            // Returns immediately on the host: ReceiveGameMessage is CallLocal = false, so the host
            // never queues its own messages. The wait is what a client needs.
            await ChangeEventQueue.Instance.WhenDrained();
        }
        catch (Exception e)
        {
            ErrorReporter.Report(e, "MultiplayerSession.HideLoadingScreenWhenReady");
        }
        finally
        {
            // In finally: a failed drain must never strand the player behind an opaque cover, nor leave a
            // peer stuck fast-forwarding for the rest of the game. By the time this Rpc arrives every
            // replayed message is already in this peer's queue, so the drain above is exact.
            ReplayContext.EndFastForward();
            if (afterRestore && !Multiplayer.IsServer())
                _restore.FinishClientRestore();

            // Unit placement is the one piece of view state a suppressed animation owns (GameAPI enqueues
            // DeployUnitAnimation, and CountryScene.AddUnit lives inside it), so a restored board would
            // otherwise come up with no pieces on it. Rebuilt from state here, on every peer, at the last
            // moment before the cover lifts. Passed as an argument rather than read off IsFastForwarding
            // because the host clears that at the end of its replay, before this Rpc goes out.
            if (afterRestore)
                PresentationServices.World.PlaceDeployedUnits();

            GameManager.Instance?.HideLoadingScreen();
        }
    }

    /// <summary>
    /// Phase A of the synchronized game-end transition. Broadcast by the host when a win
    /// condition is met. Every peer stashes the result, waits for its OWN change-event and
    /// animation pipelines to drain (so a slower client finishes the final turn's effects
    /// first), then reports ready. The host only proceeds once all peers have reported.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public async void BeginEndGame(string resultJson)
    {
        try
        {
            DebugUtilities.PrintPeer("BeginEndGame received — draining local queues before victory screen");
            VictoryScreen.PendingResult = JsonSerializer.Deserialize<GameResult>(resultJson);

            // Before the drain, not after: a headless run has no victory screen to reach, and this
            // is its only game-over notification. Announcing it here lets an automated run report the
            // result and exit instead of paying for a scene switch it will never look at.
            EventBus.Emit(EventBus.SignalName.GameEnded, resultJson);

            // Drain change events first (applying one may enqueue animations), then animations.
            // WhenDrained() replaces `if (!IsIdle) await ToSignal(QueueDrained)`, which was a
            // check-then-await race that hung forever if the queue drained in between.
            await ChangeEventQueue.Instance.WhenDrained();
            await AnimationQueue.Instance.Start();
        }
        catch (Exception e)
        {
            // Report but still report ready: a peer that fails to drain must not strand every other
            // peer on the readiness barrier and leave the victory screen unreachable for everyone.
            ErrorReporter.Report(e, "MultiplayerSession.BeginEndGame");
        }
        finally
        {
            _endGameReadiness.RegisterReady();
        }
    }

    /// <summary>Phase B: runs on the host once host + all clients have drained and reported ready.</summary>
    private void OnAllPeersReadyForVictory()
    {
        if (!Multiplayer.IsServer()) return;
        DebugUtilities.PrintPeer("All peers ready — switching everyone to the victory screen");
        Rpc(nameof(PerformVictorySwitch));
    }

    /// <summary>Final step: every peer switches to the victory screen at the same time.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void PerformVictorySwitch()
    {
        SceneFlow.ChangeScene(this, "res://scenes/menu/VictoryScreen.tscn");
    }
}
