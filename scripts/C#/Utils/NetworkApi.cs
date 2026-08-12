using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public partial class NetworkApi : Node
{
    
    public static NetworkApi Instance { get; private set; }
    

    [Signal] public delegate void MultiplayerSessionStartedEventHandler();
    [Signal] public delegate void PlayersLoadedEventHandler();

    public override void _Ready()
    {
        Instance = this;
        Multiplayer.PeerDisconnected += OnPeerDisconnectedDuringInput;
    }

    public override void _ExitTree()
    {
        if (Multiplayer != null)
            Multiplayer.PeerDisconnected -= OnPeerDisconnectedDuringInput;
    }
    
    public async Task StartMultiplayerSession(string configuration)
    {
        if (!Multiplayer.IsServer()) return;

        var sessionStarted = ToSignal(this, SignalName.MultiplayerSessionStarted);
        Rpc(nameof(LoadMultiplayerSession));
        await sessionStarted;

        var playersLoaded = ToSignal(this, SignalName.PlayersLoaded);
        Rpc(nameof(LoadPlayers), configuration);
        await playersLoaded;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void LoadMultiplayerSession()
    {
        MultiplayerSession multiplayerSessionInstance = AssetRepository.multiplayerSessionScenePacked.Instantiate<MultiplayerSession>();
        multiplayerSessionInstance.SetMultiplayerAuthority(1); // host is authority     
        if (Multiplayer.IsServer())
        {            
            PeerReadinessComponent peerReadinessComponent = multiplayerSessionInstance.GetNode<PeerReadinessComponent>("PeerReadinessComponent");
            peerReadinessComponent.AllPeersReady += ()=>{ EmitSignal(SignalName.MultiplayerSessionStarted); };
        }         
        AddChild(multiplayerSessionInstance, true);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void LoadPlayers(string configuration)
    {
        PlayerFactionRegistry.Clear();
        List<PlayerFactionAssignment> playerFactionAssignments = JsonSerializer.Deserialize<List<PlayerFactionAssignment>>(configuration);
        // Step 2: Create and register all players
        int remaining = playerFactionAssignments.Count;
        foreach (var assignment in playerFactionAssignments)
        {
            int peerId = assignment.PeerId;
            List<Faction> factions = assignment.Factions;
            
            string factionNames = string.Join(", ", factions);
            DebugUtilities.PrintPeerFinest($"Creating player for peer {peerId} with factions: {factionNames}");
            
            PlayerScene player = AssetRepository.PlayerScenePackged.Instantiate<PlayerScene>();
            player.SetMultiplayerAuthority(peerId);
            player.PlayerName = $"Player_{peerId}";

            if (Multiplayer.IsServer())
            {
                PeerReadinessComponent peerReadinessComponent = player.GetNode<PeerReadinessComponent>("PeerReadinessComponent");
                peerReadinessComponent.AllPeersReady += () => { if (--remaining == 0) EmitSignal(SignalName.PlayersLoaded); };
            }

            NodeUtilities.Instance.PlayersNode.AddChild(player);

            PlayerFactionRegistry.RegisterPlayer(player);
            
            DebugUtilities.PrintPeerFinest($"Player {peerId} added to scene tree");
        }
        
        // Step 3: Assign factions to players
        foreach (var assignment in playerFactionAssignments)
        {
            int peerId = assignment.PeerId;
            List<Faction> factions = assignment.Factions;
            
            DebugUtilities.PrintPeer($"Assigning {factions.Count} faction(s) to peer {peerId}");
            PlayerFactionRegistry.AssignFactionsToPlayer(peerId, factions);
        }
    }

    /// <summary>
    /// Called on all clients by the server after a GameMessage is applied.
    /// Clients reconstruct the message, queue it for replay in wire order, and — for a state mutation —
    /// verify the state hash once it has been applied.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveGameMessage(string dtoJson)
    {
        // Was `async void` with no catch (it never actually awaited): a malformed payload or an
        // unregistered DTO type (GameMessage.FromDto throws NotSupportedException) killed the client
        // process outright.
        try
        {
            ReceiveGameMessageInternal(dtoJson);
        }
        catch (Exception e)
        {
            ErrorReporter.Report(e, "Rpc ReceiveGameMessage");
        }
    }

    private void ReceiveGameMessageInternal(string dtoJson)
    {
        GameMessageDto dto = JsonSerializer.Deserialize<GameMessageDto>(dtoJson);
        GameMessage    msg = GameMessage.FromDto(dto);
        DebugUtilities.PrintPeer($"[color={"blue"}]ReceiveGameMessage ({msg.Id}): {dto.GetType().Name}");
        DebugUtilities.PrintPeerFinest($"{dtoJson}");

        // Only a state mutation has a hash worth comparing. Gating on the type rather than on
        // "HashAfterApplication != null" is deliberate: a PresentationEvent has no such field at all,
        // so there is nothing to forget to stamp and nothing to accidentally compare — while every real
        // ChangeEvent still gets checked, because every ChangeEvent still has the field.
        if (msg is ChangeEvent ev)
            ev.ChangeEventApplied += (id) => VerifyReplicatedHash(ev, dto);

        ChangeEventQueue.Instance.Enqueue(msg);
    }

    private void VerifyReplicatedHash(ChangeEvent ev, GameMessageDto dto)
    {
        string actualHash = MultiplayerSession.Instance.GameState.ComputeHash();
        if (actualHash != ev.HashAfterApplication)
        {
            DebugUtilities.PrintPeerError($"///////////////////////////////////////////////////////////////////////////");
            DebugUtilities.PrintPeerError($"DESYNC DETECTED after applying {dto.GetType().Name}!");
            DebugUtilities.PrintPeerError($"Hash mismatch after {dto.GetType().Name}: expected {ev.HashAfterApplication}, got {actualHash}. Requesting resync.");
            DebugUtilities.PrintPeerError($"EventId: {ev.Id}, LatestAppliedId: {ChangeEvent.LatestAppliedId}");
            DebugUtilities.PrintPeerError($"///////////////////////////////////////////////////////////////////////////");
            DebugUtilities.PrintPeerError($"{JsonSerializer.Serialize(MultiplayerSession.Instance.GameState)}");
            RpcId(1, nameof(RequestResync));
            return;
        }
    }

    // ── Input-request rendezvous ─────────────────────────────────────────────
    //
    // This used to be `await EventBus.ToSignal(InputRequestResponseReceived)`: untimed and, worse,
    // UNCORRELATED — a bare global signal carrying no request id. An abandoned awaiter from a failed
    // step stayed registered, so a later response resumed the DEAD continuation and walked the old
    // card pipeline concurrently with the recovered loop (double Apply, double Rpc, guaranteed
    // desync). It is now a TaskCompletionSource keyed on InputRequest.Id, with a bounded wait so a
    // client that cannot reply at all (e.g. FromJson threw, so it cannot even tell whether the
    // request was for it) does not hang the host forever.

    /// <summary>
    /// Backstop only, for the case where the controlling peer can never reply at all — deliberately
    /// long, because a real player is allowed to deliberate. A peer that drops is unblocked
    /// immediately by <see cref="OnPeerDisconnectedDuringInput"/> instead of waiting this out.
    ///
    /// Expiring does NOT decide anything by itself: it releases the client's prompt and hands the host a
    /// Retry / Skip choice, while this method keeps holding the step's await. It used to mark the request
    /// skipped and return, which silently advanced the turn step out from under a player who was still
    /// being asked — including past a MANDATORY discard, leaving the hand over the limit.
    ///
    /// Replicated to the client as <c>InputRequest.TimeoutSeconds</c> so it can show the countdown; a
    /// deadline the player cannot see is a deadline they cannot act on.
    /// </summary>
    ///
    /// Effectively disabled in CLI mode. The backstop assumes a human who will eventually click, and
    /// ErrorReporter.ReportInputTimeoutAndAwaitDecision returns "skip" immediately when headless (no
    /// popup to ask with) — so a developer thinking at a terminal for 15 minutes would silently lose
    /// the turn step, with no way to tell that from the game having moved on.
    private static int InputResponseTimeoutMs
        => GameContext.IsCli ? int.MaxValue : 15 * 60 * 1000;

    /// <summary>The same window in minutes, for the message on the timeout popup.</summary>
    public static int InputResponseTimeoutMinutes => InputResponseTimeoutMs / 60000;

    private TaskCompletionSource<string> _pendingInputTcs;
    private string _pendingInputId = null;
    private int _pendingInputPeer = 0;

    /// <summary>
    /// The request we are waiting on. Kept so cancellation can complete the wait with a real
    /// (empty, skipped) response instead of cancelling the task: an empty response is exactly how a
    /// player "passes", so both callers unwind correctly — <c>RequestPlay</c> reads no card ids and
    /// treats it as a pass, while <c>InputRequest.BroadCast</c> sees WasSkipped and raises
    /// StepSkippedException, which CardStep already handles. Cancelling the task instead would
    /// unwind the play step without ever firing CardPlayPoolFinished, stalling the turn loop.
    /// </summary>
    private InputRequest _pendingInputRequest;

    /// <summary>
    /// Completing this expires the current attempt's wait ahead of its deadline — the host's
    /// "time out now" button on <see cref="InputTimerDisplay"/>. A TCS raced in the same
    /// <c>Task.WhenAny</c> as the backstop delay rather than a shortcut around it, so a forced timeout
    /// and a real one take byte-for-byte the same path: release the prompt, then Retry / Skip.
    ///
    /// Replaced per attempt alongside <see cref="_pendingInputTcs"/>, so pressing the button during
    /// attempt 2 cannot resolve a stale signal left over from attempt 1.
    /// </summary>
    private TaskCompletionSource<bool> _forceTimeoutTcs;

    /// <summary>
    /// Host-only: expire the in-flight input request now instead of waiting out the backstop. No-op on
    /// a client (the wait it would need to expire is on the host) and when nothing is in flight.
    /// </summary>
    public void ForceInputTimeout()
    {
        if (Multiplayer?.MultiplayerPeer != null && !Multiplayer.IsServer()) return;

        TaskCompletionSource<bool> force = _forceTimeoutTcs;
        if (force == null || force.Task.IsCompleted) return;

        DebugUtilities.PrintPeer($"Host forced a timeout on the pending input request (Id {_pendingInputId})");
        force.TrySetResult(true);
    }

    public async Task<InputRequest> SendInputRequest(InputRequest inputRequest)
    {
        DebugUtilities.PrintPeer($"[color={"purple"}]SendInputRequest: {inputRequest.GetType().Name}");

        // Stamp the legal move set onto the request before it goes on the wire, so it is
        // self-describing to a scripted/CLI peer. Here rather than in InputRequest.BroadCast because
        // CardPlayRound.RequestPlay and RequestBlock call this method directly, bypassing BroadCast —
        // and those build the very requests (HandCardPlay, ActivateCard) that need it. Outside the
        // retry loop: the option set must not shift between attempts at the same prompt.
        inputRequest.PopulateTargets();

        // Loop so Retry re-sends THIS request. Nothing unwinds between attempts: the caller's await is
        // still parked here, which is exactly what keeps the turn loop from advancing while the host
        // decides. Retrying at the step level instead would replay non-idempotent work — a mutator that
        // already removed a unit would remove a second one.
        while (true)
        {
            // A fresh Id per attempt. Aborting the previous attempt makes the client's handler complete
            // as skipped and reply; that reply carries the OLD Id, so ReceiveInputResponse's staleness
            // check drops it instead of instantly resolving the retry we are about to open.
            inputRequest.Id = Guid.NewGuid().ToString();
            inputRequest.TimeoutSeconds = InputResponseTimeoutMs / 1000;

            string payload = inputRequest.ToJson();
            DebugUtilities.PrintPeerFinest($"{payload}");

            // Create the awaiter BEFORE the Rpc — the same ordering StartMultiplayerSession uses, and
            // the reason it is the one rendezvous in this file without a check-then-await race.
            TaskCompletionSource<string> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> forceTimeout = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingInputTcs = tcs;
            _pendingInputId = inputRequest.Id;
            _pendingInputPeer = SafeTargetPeer(inputRequest);
            _pendingInputRequest = inputRequest;
            _forceTimeoutTcs = forceTimeout;

            Rpc(nameof(NetworkApi.ReceiveInputRequest), payload);

            // Cancelled on the normal path so a long game does not accumulate one live 15-minute timer
            // per input request; the old bare Task.Delay was never cancelled.
            using CancellationTokenSource timeoutCts = new();
            Task completed = await Task.WhenAny(
                tcs.Task, forceTimeout.Task, Task.Delay(InputResponseTimeoutMs, timeoutCts.Token));
            timeoutCts.Cancel();

            if (completed == tcs.Task)
            {
                ClearPendingInput(tcs);
                return InputRequest.FromJson(await tcs.Task);
            }

            bool wasForced = completed == forceTimeout.Task;
            DebugUtilities.PrintPeerErrorRaw(wasForced
                ? $"Host timed out {inputRequest.GetType().Name} (Id {inputRequest.Id}) by hand — " +
                  "holding the turn loop for a Retry / Skip decision."
                : $"No input response for {inputRequest.GetType().Name} (Id {inputRequest.Id}) after " +
                  $"{InputResponseTimeoutMinutes} minutes — holding the turn loop for a Retry / Skip decision.");

            // Order matters: drop the awaiter FIRST, then abort. AbortRemoteInput makes the client reply
            // straight away, and with the awaiter already cleared that reply is ignored ("no request
            // pending") rather than resolving a wait we may be about to re-open.
            ClearPendingInput(tcs);
            AbortRemoteInput();
            GameFlow.Instance?.ClearCurrentInputRequest();

            if (await ErrorReporter.ReportInputTimeoutAndAwaitDecision(inputRequest, wasForced)) continue;

            // Skip: unchanged from the old behaviour, but now a deliberate choice rather than a silent
            // one. BroadCast turns this into StepSkippedException, which the step handlers already treat
            // as the player passing.
            inputRequest.WasSkipped = true;
            return inputRequest;
        }
    }

    private static int SafeTargetPeer(InputRequest inputRequest)
    {
        try { return inputRequest.TargetPeer; }
        catch { return 0; }
    }

    /// <summary>
    /// If the peer we are waiting on for input drops, complete the wait as skipped straight away.
    /// Otherwise a mid-turn disconnect left the host parked on the request until the backstop
    /// timeout — indistinguishable from a freeze.
    /// </summary>
    private void OnPeerDisconnectedDuringInput(long peerId)
    {
        if (Multiplayer?.MultiplayerPeer == null || !Multiplayer.IsServer()) return;
        if (_pendingInputTcs == null) return;
        if (_pendingInputPeer != 0 && _pendingInputPeer != (int)peerId) return;

        DebugUtilities.PrintPeerErrorRaw(
            $"Peer {peerId} disconnected while we were waiting on its input — treating as skipped.");
        CancelPendingInputRequest();
    }

    /// <summary>
    /// Complete the in-flight input request as skipped so the awaiting chain unwinds through the
    /// existing StepSkippedException path. Called by the recovery sweep.
    /// </summary>
    public void CancelPendingInputRequest()
    {
        TaskCompletionSource<string> tcs = _pendingInputTcs;
        if (tcs == null || tcs.Task.IsCompleted) return;

        DebugUtilities.PrintPeer($"Cancelling pending input request (Id {_pendingInputId})");

        // Complete with the request itself, marked skipped and carrying no responses — see the note on
        // _pendingInputRequest for why this rather than TrySetCanceled.
        InputRequest skipped = _pendingInputRequest;
        if (skipped != null)
        {
            skipped.WasSkipped = true;
            tcs.TrySetResult(skipped.ToJson());
        }
        else
        {
            tcs.TrySetCanceled();
        }
        ClearPendingInput(tcs);
        GameFlow.Instance?.ClearCurrentInputRequest();
    }

    private void ClearPendingInput(TaskCompletionSource<string> tcs)
    {
        if (ReferenceEquals(_pendingInputTcs, tcs))
        {
            _pendingInputTcs = null;
            _pendingInputId = null;
            _pendingInputPeer = 0;
            _pendingInputRequest = null;
            // Dropped with the rest: with no request in flight the host's "time out now" button has
            // nothing to expire, and leaving a live TCS here would let a press arm the NEXT attempt.
            _forceTimeoutTcs = null;
        }
    }

    /// <summary>
    /// Tell every client to release any open board selection, so a client sitting on a
    /// SelectCountry/SelectUnit prompt for an aborted step does not stay stuck on it.
    /// </summary>
    public void AbortRemoteInput()
    {
        // No session at all (single process, or the peer already torn down): there is nobody to Rpc, but
        // this process may still be sitting on its own prompt, so release it directly.
        if (Multiplayer?.MultiplayerPeer == null)
        {
            AbortInputRequest();
            return;
        }
        if (!Multiplayer.IsServer()) return;
        Rpc(nameof(AbortInputRequest));
    }

    /// <summary>Server → all peers: cancel any local board selection currently awaiting a click.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void AbortInputRequest()
    {
        PendingLocalInput.CancelAll();
        PresentationModal.Current?.CancelPending();

        // Covers the peers that were only watching: their Execute() took the "Waiting on X" branch and
        // returned, so nothing local will ever clear their countdown.
        InputTimerDisplay.Current?.Hide();
    }

    /// <summary>
    /// Server → all peers: the pending input has been answered, so stop the countdown everywhere.
    ///
    /// Needed because the answering peer's <c>ReceiveInputResponse</c> does not reach the other clients —
    /// an AnyPeer Rpc from a client only reaches the server (see <see cref="ReportErrorToServer"/> for the
    /// same asymmetry). Without this, every peer that was merely watching would count its countdown down
    /// to 0:00 and leave it stranded there while play carried on.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void InputRequestAnswered()
    {
        InputTimerDisplay.Current?.Hide();
    }

    // ── Error propagation ────────────────────────────────────────────────────

    /// <summary>
    /// Client → server. A client's Rpc with AnyPeer only reaches the server, so the server has to
    /// rebroadcast for the other clients to learn about it.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReportErrorToServer(string errorJson)
    {
        if (!Multiplayer.IsServer()) return;
        try
        {
            ErrorReporter.ReportFromRemote(GameError.FromJson(errorJson));
            Rpc(nameof(BroadcastError), errorJson);
        }
        catch (Exception e)
        {
            DebugUtilities.PrintPeerErrorRaw($"Failed to relay a peer error report: {e}");
        }
    }

    /// <summary>
    /// Server → all peers. Without this, a host-side failure leaves every client staring at a
    /// frozen board with nothing on screen, since the authoritative loop runs only on the host.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void BroadcastError(string errorJson)
    {
        try
        {
            ErrorReporter.ReportFromRemote(GameError.FromJson(errorJson));
        }
        catch (Exception e)
        {
            DebugUtilities.PrintPeerErrorRaw($"Failed to ingest a broadcast error report: {e}");
        }
    }

    /// <summary>
    /// Called on all clients by the server after a ChangeEvent is applied.
    /// Clients reconstruct the event, apply it locally, then verify the state hash.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public async void ReceiveInputRequest(string dtoJson)
    {
        InputRequest dto = null;
        try
        {
            dto = InputRequest.FromJson(dtoJson);
            DebugUtilities.PrintPeer($"[color={"purple"}]ReceiveInputRequest:  {dto.GetType().Name} (For me: {dto.IsForCurrentPeer})");
            DebugUtilities.PrintPeerFinest($"{dtoJson}");

            // WhenDrained() instead of `if (!IsIdle) await ToSignal(QueueDrained)`: that was a
            // check-then-await race — if the queue drained in between, QueueDrained had already
            // fired and the await hung forever.
            await ChangeEventQueue.Instance.WhenDrained();
            if (dto.IsForCurrentPeer && !Multiplayer.IsServer())
                ErrorInjection.MaybeThrow(ErrorInjection.Site.ClientInput, dto.GetType().Name);
            await dto.Execute();

            if (dto.IsForCurrentPeer)
            {
                Rpc(nameof(ReceiveInputResponse), dto.ToJson());
            }
        }
        catch (Exception e)
        {
            ErrorReporter.Report(e, "Rpc ReceiveInputRequest", dto?.TargetFaction);

            // Still reply, marked skipped, so the host's await unwinds through the existing
            // StepSkippedException path instead of waiting forever. If `dto` is null the request
            // could not even be parsed — we cannot know whether it was for this peer, so there is
            // nothing safe to send and the host's bounded wait is the only cover.
            if (dto != null && dto.IsForCurrentPeer)
            {
                try
                {
                    dto.WasSkipped = true;
                    Rpc(nameof(ReceiveInputResponse), dto.ToJson());
                }
                catch (Exception replyFailure)
                {
                    DebugUtilities.PrintPeerErrorRaw($"Could not send skip reply: {replyFailure}");
                }
            }
        }
    }


    

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveInputResponse(string dtoJson)
    {
        // Was `async void` with no catch: any throw here went to the synchronization context and
        // took the process down.
        try
        {
            if (!Multiplayer.IsServer())
            {
                PlayerActionLabel.HideText();
                InputTimerDisplay.Current?.Hide();
                return;
            }

            InputRequest response = InputRequest.FromJson(dtoJson);

            // Drop responses that belong to a request we are no longer waiting on, so a late reply
            // from an aborted step cannot resume a dead continuation.
            TaskCompletionSource<string> tcs = _pendingInputTcs;
            if (tcs == null)
            {
                DebugUtilities.PrintPeer($"Ignoring input response {response?.Id} — no request pending");
                return;
            }
            if (response != null && _pendingInputId != null && response.Id != _pendingInputId)
            {
                DebugUtilities.PrintPeer(
                    $"Ignoring stale input response (Id {response.Id}, waiting on {_pendingInputId})");
                return;
            }

            // Assigned only once the response is known to be the one we are waiting on. It used to be
            // set above the guards, so a late reply to a timed-out request still overwrote the slot.
            GameFlow.Instance.CurrentInputRequest = response;

            // Clear the countdown on every peer, not just here. Below the guards on purpose: doing it for
            // a stale reply would wipe the live countdown of a retry already in progress.
            if (Multiplayer?.MultiplayerPeer != null)
                Rpc(nameof(InputRequestAnswered));
            else
                InputRequestAnswered();

            tcs.TrySetResult(dtoJson);

            // Kept for UI/debug listeners that were already observing this signal.
            EventBus.Emit(EventBus.SignalName.InputRequestResponseReceived, dtoJson);
        }
        catch (Exception e)
        {
            ErrorReporter.Report(e, "Rpc ReceiveInputResponse");
        }
    }

    // ShowPlayerActionLabel used to live here: an Rpc that broadcast a label to every peer. It ran
    // immediately in the receiving frame while replicated messages sat deferred in ChangeEventQueue, so
    // the label regularly appeared ahead of the effects it described. Its callers now use
    // ShowActionLabelPresentationEvent, which rides the ordered stream instead. Broadcast a label by
    // applying one of those, not by adding an Rpc back.

    /// <summary>Client → server: request a full state snapshot due to hash mismatch.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestResync()
    {
        if (!Multiplayer.IsServer()) return;
        int requestingPeer = Multiplayer.GetRemoteSenderId();
        DebugUtilities.PrintPeer($"Resync requested by peer {requestingPeer}");
        string dto = JsonSerializer.Serialize(MultiplayerSession.Instance.GameState);

        DebugUtilities.PrintPeerError($"///////////////////////////////////////////////////////////////////////////");
        DebugUtilities.PrintPeerError($"DESYNC DETECTED!");
        DebugUtilities.PrintPeerError($"///////////////////////////////////////////////////////////////////////////");
        DebugUtilities.PrintPeerError($"{JsonSerializer.Serialize(MultiplayerSession.Instance.GameState)}");
        
        
        //RpcId(requestingPeer, nameof(ReceiveFullSnapshot), dto);
    }

    /// <summary>Server → client: delivers a full state snapshot in response to RequestResync.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveFullSnapshot(string snapshotJson)
    {        
        MultiplayerGameStateSnapshot snapshot = JsonSerializer.Deserialize<MultiplayerGameStateSnapshot>(snapshotJson);
        MultiplayerSession.Instance.GameState.ApplySnapshot(snapshot);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveGameStateUpdate(string gameStateJson, string changeEventType)
    {
        DebugUtilities.PrintPeer($"ReceiveGameStateUpdate: {changeEventType}");
        MultiplayerGameStateSnapshot snapshot = JsonSerializer.Deserialize<MultiplayerGameStateSnapshot>(gameStateJson);
        MultiplayerSession.Instance.GameState.ApplySnapshot(snapshot);
        EventBus.Emit(EventBus.SignalName.GameChangeEventAfter, changeEventType);
    }
}
