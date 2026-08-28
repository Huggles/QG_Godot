using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            DebugUtilities.PrintPeer($"RNG seed: {GameRandom.Seed}");

            // Consumed here rather than read repeatedly, so a failure part-way through cannot leave the
            // next game trying to restore this save again. Host-only: PendingSave is never set on a
            // client, and a client rebuilds a restored game from the broadcast stream like any other.
            SaveGame restore = Multiplayer.IsServer() ? GameManager.PendingSave : null;
            GameManager.PendingSave = null;

            GameModeMultiplayerDefault gameMode = new GameModeMultiplayerDefault();
            await gameMode.Init();
            DebugUtilities.PrintPeerFinest("Game mode initialization complete, emitting MultiplayerSessionReady");


            if(Multiplayer.IsServer())
            {
                if (restore != null)
                {
                    // The HUD goes up BEFORE the replay rather than after it. A restore applies its whole
                    // event log in a couple of frames, so a HUD built afterwards would miss every signal
                    // that populates it, and the prompt the resume raises could reach a null
                    // ModalStack.Current. Building first and letting the events stream into it is exactly
                    // what every client already does with the setup burst.
                    PlayerScene.Current?.EnsureUiLoaded();

                    ReplayContext.BeginFastForward();
                    Rpc(nameof(BeginFastForward));
                    await RestoreSavedGame(gameMode, restore);
                }
                else
                {
                    await gameMode.InitStartingState();
                    GameFlow.Instance.StartGame();
                }
            }


            // Build the local HUD underneath the loading cover, which is still up.
            // Null on a dedicated/headless server (it controls no faction, so has no local PlayerScene).
            PlayerScene.Current?.EnsureUiLoaded();
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

                // After the cover comes down, so the player sees the restored board a beat before the
                // prompt it was saved on reappears over it. Last, because it is the one call here that
                // starts the turn loop running again.
                if (restore != null)
                    GameFlow.Instance.ResumeAfterLoad();
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
    /// <summary>
    /// Tell every client to stop animating: the host is about to replay a saved game and push its whole
    /// event log at them in a few frames.
    ///
    /// CallLocal = false because the host sets its own flag directly. Ordering is guaranteed rather than
    /// hoped for: NetworkApi.ReceiveGameMessage enqueues synchronously in the Rpc body, so a reliable
    /// Rpc sent before the burst runs before any of it is queued.
    ///
    /// Cleared again in HideLoadingScreenWhenReady, NOT on a queue drain — ChangeEventQueue.WhenDrained
    /// means "the queue is empty right now", and a client that out-runs the host empties it repeatedly
    /// mid-burst.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void BeginFastForward() => ReplayContext.BeginFastForward();

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

    // ══════════════════════════════════════════════════════════════════════════
    // Save / load
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Host-only: capture the game to disk and return the file path, or null if it could not be taken.
    ///
    /// Everything here must be read synchronously, in one go, before anything is awaited. The escape
    /// menu deliberately does not pause the tree (ErrorPopup documents why), so the turn loop keeps
    /// running while the menu is open — but it runs on this same thread, so a straight-line capture is
    /// atomic with respect to it. Await first and the log grows underneath you, leaving a save whose
    /// events, flow snapshot and hash describe three different moments.
    /// </summary>
    /// <param name="deferred">
    /// True when this is a save the player asked for at an unsaveable moment and GameFlow has just
    /// flushed at the next boundary. Only affects the log line and the toast.
    /// </param>
    public string CaptureSave(string displayName, bool deferred = false)
    {
        if (!Multiplayer.IsServer())
        {
            DebugUtilities.PrintPeerError("CaptureSave: only the host can save the game");
            return null;
        }

        if (!GameFlow.Instance.CanSave)
        {
            DebugUtilities.PrintPeerErrorRaw($"CaptureSave: cannot save right now — {GameFlow.Instance.SaveBlockedReason}");
            return null;
        }

        SaveGame save;
        try
        {
            save = BuildSave(displayName);
        }
        catch (Exception e)
        {
            // Raw, and with the trace: a save that throws while assembling is a bug in a ToDto somewhere,
            // and the one thing the player must not get is a silent no-op.
            DebugUtilities.PrintPeerErrorRaw($"CaptureSave: failed to assemble the save: {e}");
            return null;
        }

        string path = SaveGameService.Save(save);

        if (path != null)
            EventBus.Emit(EventBus.SignalName.GameSaved, displayName, deferred);

        return path;
    }

    /// <summary>The synchronous half of <see cref="CaptureSave"/>. See its remarks on why.</summary>
    private SaveGame BuildSave(string displayName)
    {
        return new SaveGame
        {
            // UTC with the ISO "T" separator, so an ordinal string sort is a chronological sort and the
            // load list needs no parsing to order itself. Rendered in local time for display.
            SavedAtIso   = Time.GetDatetimeStringFromSystem(utc: true),
            DisplayName  = displayName,

            // The scenario travels inside the save rather than as a path, so restoring does not depend
            // on the file still existing, still being at that path, or still having the same contents.
            ScenarioJsonBase64 = GameManager.ActiveScenarioJson == null
                ? null
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(GameManager.ActiveScenarioJson)),
            ScenarioTitle  = GameManager.ActiveScenarioTitle,
            ScenarioPath   = GameManager.PendingScenarioPath,
            OpeningDiscard = GameFlow.Instance.OpeningDiscardEnabled,
            Seed           = GameRandom.Seed,
            RngDraws       = GameRandom.DrawCount,

            // OfType, not a cast: the journal also holds PresentationEvents (which mutate nothing) and
            // RecalculateTagsMessages (derived state the host recomputes per event during replay).
            Events = GameState.GameMessages.OfType<ChangeEvent>()
                              .Select(changeEvent => (GameMessageDto)changeEvent.ToDto())
                              .ToList(),

            Flow         = GameFlow.Instance.BuildFlowSnapshot(),
            Resume       = GameFlow.Instance.IsAtStepStart
                               ? ResumeMode.ReRunCurrentStep
                               : ResumeMode.NextStep,
            PendingInput = NetworkApi.Instance.DescribePendingInputs().FirstOrDefault(),
            StateHash    = GameState.ComputeHash(),
            Seats        = BuildSeats()
        };
    }

    /// <summary>
    /// Who held which factions, for the lobby to pre-fill on load. Never enforced — the replay is
    /// entirely host-side and knows nothing about peers, so the players may reshuffle freely.
    /// </summary>
    private static List<SavedSeat> BuildSeats()
    {
        // Built from the faction-to-peer map rather than from the registry's PlayerScene objects. Those
        // are Godot nodes held in a static that outlives the game scene, so reading one can throw
        // ObjectDisposedException — and a stale node must never be able to fail a save. The map is plain
        // dictionaries, and the display name is looked up through the registry's own null-safe accessor.
        return StaticGameData.PlayableFactions
            .GroupBy(PlayerFactionRegistry.GetPeerIdForFaction)
            .OrderBy(group => group.Key)
            .Select(group => new SavedSeat
            {
                DisplayName = NameForSeat(group),
                Factions    = group.ToList()
            })
            .ToList();
    }

    private static string NameForSeat(IEnumerable<Faction> factions)
    {
        // GetDisplayNameForFaction answers null for a freed or unnamed player, so an unclaimed seat
        // simply goes in unnamed and matches by join order on load.
        foreach (Faction faction in factions)
        {
            string name = PlayerFactionRegistry.GetDisplayNameForFaction(faction);
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return null;
    }

    /// <summary>
    /// Host-only: rebuild a saved game by REPLAYING its ChangeEvent log onto the freshly-built shell.
    ///
    /// Replaying runs the real pipeline — broadcast on, animations suppressed — so the board, decks,
    /// victory points, modifiers, card history, the history panel and every client's state all
    /// reconstruct through the live code rather than through a parallel deserializer. Each event's DTO
    /// carries the outcome it produced the first time, so this is re-applying decisions rather than
    /// re-making them, and it does not depend on the game being deterministic.
    ///
    /// The caller resumes the turn machine; this method only rebuilds state.
    /// </summary>
    private async Task RestoreSavedGame(GameModeMultiplayerDefault gameMode, SaveGame save)
    {
        DebugUtilities.PrintPeer(
            $"RestoreSavedGame: '{save.DisplayName}' — replaying {save.Events.Count} event(s)");

        ReplayContext.IsReplaying = true;
        try
        {
            // The half of setup that is NOT expressed as ChangeEvents and so is not in the log: MaxRound,
            // the opening-discard flag, and the scenario's step mutators. Activatable mutators are
            // skipped because those DO reach the log, as RegisterBulletinCardChangeEvents.
            await gameMode.ConfigureFromScenario();

            // Every event recalculates tags on the way through, exactly as it did the first time.
            GameStateCalculator.Enabled = true;

            int applied = 0;
            foreach (GameMessageDto dto in save.Events)
            {
                if (GameMessage.FromDto(dto) is not ChangeEvent changeEvent)
                {
                    DebugUtilities.PrintPeerErrorRaw(
                        $"RestoreSavedGame: skipping a non-ChangeEvent in the log ({dto.GetType().Name})");
                    continue;
                }

                await changeEvent.Apply();

                // Let the frame breathe. With animations suppressed and durations zeroed, the whole log
                // would otherwise apply inside a single frame: the window stops repainting, Windows marks
                // it unresponsive, and the loading cover cannot show progress. Also gives the deferred
                // UI construction from EnsureUiLoaded its frames to actually run.
                if (++applied % ReplayYieldInterval == 0)
                {
                    ReportRestoreProgress(applied, save.Events.Count);
                    await Task.Yield();
                }
            }
        }
        finally
        {
            ReplayContext.IsReplaying = false;
        }

        // Replayed messages keep their original ids, which ChangeEvent.ForId depends on. Push the
        // counter past them so the first live message after the restore does not collide.
        GameMessage.SyncCounterToLatest();

        // Put the random stream back where the save left it, rather than handing the continuation the
        // numbers the opening shuffle already used.
        GameRandom.Initialize(save.Seed);
        GameRandom.FastForward(save.RngDraws);

        GameFlow.Instance.ApplyFlowSnapshot(save.Flow, save.Resume);
        GameStateCalculator.CalculateAll();

        string hash = GameState.ComputeHash();
        // Raw, so it survives the CLI console muting. A replay that quietly produced a different board
        // than the one that was saved is the single worst outcome this feature has, and it must never be
        // something you only find out about by noticing the game is wrong.
        if (save.StateHash != null && hash != save.StateHash)
            DebugUtilities.PrintPeerErrorRaw(
                $"RestoreSavedGame: replay diverged from the save — expected hash {save.StateHash}, got {hash}. " +
                "An event re-decided an outcome instead of re-applying the recorded one.");
        else
            DebugUtilities.PrintPeer($"RestoreSavedGame: replay complete, hash {hash} matches the save.");

        // Handed to NetworkApi so the first prompt the resumed step raises can be checked against the
        // one that was actually open when the save was taken.
        NetworkApi.Instance.ExpectResumedPrompt(save.PendingInput);

        ReplayContext.EndFastForward();
    }

    /// <summary>
    /// How many replayed events to apply between frame yields. Small enough that the cover keeps
    /// painting, large enough that a long game does not take a frame per event.
    /// </summary>
    private const int ReplayYieldInterval = 25;

    private void ReportRestoreProgress(int applied, int total)
        => Rpc(nameof(SetRestoreProgress), $"Restoring… {applied} / {total}");

    /// <summary>
    /// Line under the loading cover while a save is being restored.
    ///
    /// A bare Rpc, so on a client it runs in the receiving frame and therefore leads the messages that
    /// client is still applying. For a progress string that is fine, and it is why this is sent every
    /// N events rather than every event — one reliable packet per ChangeEvent would be thousands.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SetRestoreProgress(string text) => GameManager.Instance?.SetLoadingStatus(text);
}

