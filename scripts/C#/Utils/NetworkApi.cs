using Godot;
using System;
using System.Collections.Concurrent;
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

            // PlayerName also becomes the Godot node Name, which is part of the NodePath RPCs resolve
            // against — so it stays a deterministic identifier. The lobby name goes on DisplayName
            // instead. Both set before AddChild so they are in place when _Ready runs.
            player.PlayerName  = $"Player_{peerId}";
            player.DisplayName = assignment.DisplayName;

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

    // ── Readiness barriers ───────────────────────────────────────────────────
    //
    // Why a barrier report lands on this autoload instead of on the barrier node itself: a client used
    // to send RpcId(1, …) ON its own PeerReadinessComponent, and Godot routes an RPC by resolving the
    // sender's absolute NodePath ON THE RECEIVER. Peers enter Game.tscn at independent times
    // (SceneFlow defers the change, and windowed peers additionally wait a frame that headless peers
    // skip), so a fast client's report regularly arrived while the host was still in the lobby:
    // "Node not found: Game/PeerReadinessComponent", packet dropped, no retry, that peer's readiness
    // lost for good. CheckAllReady then never reached `expected`, AllPeersReady never fired, and the
    // game hung behind the loading cover — reliably so at 6 peers.
    //
    // /root/NetworkApi is an autoload: it exists from the first frame of the process on every peer, so
    // a report addressed here always has somewhere to land. If the target barrier is not up yet the
    // report is parked below and drained by that barrier's own _Ready. Do not reintroduce a
    // path-routed barrier RPC.

    /// <summary>
    /// Host-only: reports for barriers that do not exist on this peer yet, keyed by barrier id.
    /// Drained by <see cref="PeerReadinessComponent"/> when it comes up.
    /// </summary>
    private static readonly Dictionary<string, HashSet<int>> _bufferedBarrierReports = new();

    /// <summary>Client → host: this peer has reached <paramref name="barrierId"/>.</summary>
    public void ReportBarrierReady(string barrierId)
        => RpcId(1, nameof(NotifyBarrierReady), barrierId);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyBarrierReady(string barrierId)
    {
        if (!Multiplayer.IsServer()) return;
        int peerId = Multiplayer.GetRemoteSenderId();

        if (PeerReadinessComponent.Deliver(barrierId, peerId)) return;

        DebugUtilities.PrintPeerFinest(
            $"Parking ready report from peer {peerId}: barrier {barrierId} has not been created here yet");
        if (!_bufferedBarrierReports.TryGetValue(barrierId, out HashSet<int> parked))
            _bufferedBarrierReports[barrierId] = parked = new HashSet<int>();
        parked.Add(peerId);
    }

    /// <summary>Hand over — and forget — the reports parked for <paramref name="barrierId"/>.</summary>
    internal static IEnumerable<int> TakeBufferedBarrierReports(string barrierId)
        => barrierId != null && _bufferedBarrierReports.Remove(barrierId, out HashSet<int> parked)
            ? parked
            : Array.Empty<int>();

    /// <summary>
    /// Drop everything still parked. Called when a session ends, so a barrier that never materialised
    /// in the last game cannot pre-satisfy the same barrier in the next one.
    /// </summary>
    public static void ClearBufferedBarrierReports() => _bufferedBarrierReports.Clear();

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

    /// <summary>
    /// One in-flight input request the host is parked on.
    /// </summary>
    private sealed class PendingInput
    {
        /// <summary>Resolved with the response JSON when the controlling peer answers.</summary>
        public TaskCompletionSource<string> Tcs;

        /// <summary>
        /// Completing this expires this attempt's wait ahead of its deadline — the host's
        /// "time out now" button on <see cref="InputTimerDisplay"/>. A TCS raced in the same
        /// <c>Task.WhenAny</c> as the backstop delay rather than a shortcut around it, so a forced
        /// timeout and a real one take byte-for-byte the same path: release the prompt, then
        /// Retry / Skip. Replaced per attempt alongside <see cref="Tcs"/>, so pressing the button
        /// during attempt 2 cannot resolve a stale signal left over from attempt 1.
        /// </summary>
        public TaskCompletionSource<bool> ForceTimeout;

        /// <summary>The peer expected to answer, so a disconnect can release only its own waits.</summary>
        public int Peer;

        /// <summary>
        /// The request we are waiting on. Kept so cancellation can complete the wait with a real
        /// (empty, skipped) response instead of cancelling the task: an empty response is exactly how
        /// a player "passes", so both callers unwind correctly — <c>RequestPlay</c> reads no card ids
        /// and treats it as a pass, while <c>InputRequest.BroadCast</c> sees WasSkipped and raises
        /// StepSkippedException, which CardStep already handles. Cancelling the task instead would
        /// unwind the play step without ever firing CardPlayPoolFinished, stalling the turn loop.
        /// </summary>
        public InputRequest Request;
    }

    /// <summary>
    /// Every input request the host is currently parked on, keyed by <see cref="InputRequest.Id"/>.
    ///
    /// A dictionary rather than the single slot this used to be, because the opening discard asks
    /// every player at once (see <c>OpeningDiscard</c>) — with one slot the second request clobbered
    /// the first and one of the two answers was dropped as stale. Normal play still only ever has one
    /// entry here; nothing else in the game asks two players anything simultaneously.
    ///
    /// Keying on the request Id keeps the staleness guarantee free: SendInputRequest mints a fresh
    /// Guid per attempt, so a late reply to an aborted attempt simply finds no entry.
    /// </summary>
    private readonly ConcurrentDictionary<string, PendingInput> _pendingInputs = new();

    /// <summary>
    /// Host-only: expire the in-flight input requests now instead of waiting out the backstop. No-op
    /// on a client (the wait it would need to expire is on the host) and when nothing is in flight.
    /// Expires every pending request: <see cref="InputTimerDisplay"/> is a single display, so the
    /// button means "give up on whatever we are waiting for", not on one particular peer.
    /// </summary>
    public void ForceInputTimeout()
    {
        if (Multiplayer?.MultiplayerPeer != null && !Multiplayer.IsServer()) return;

        foreach (KeyValuePair<string, PendingInput> entry in _pendingInputs)
        {
            TaskCompletionSource<bool> force = entry.Value.ForceTimeout;
            if (force == null || force.Task.IsCompleted) continue;

            DebugUtilities.PrintPeer($"Host forced a timeout on the pending input request (Id {entry.Key})");
            force.TrySetResult(true);
        }
    }

    /// <summary>
    /// Whether the host is currently parked on any input request.
    ///
    /// GameFlow.CanSave needs this and _pendingInputs is private. Note this is the real answer, unlike
    /// GameFlow.CurrentInputRequest, which holds the last ANSWERED response and is read by nothing.
    /// </summary>
    public bool HasPendingInput => !_pendingInputs.IsEmpty;

    /// <summary>
    /// The prompt a restored game is expected to reopen, armed by RestoreSavedGame and consumed by the
    /// first request raised afterwards. Null the rest of the time.
    /// </summary>
    private PendingPrompt _expectedResumedPrompt;

    /// <summary>
    /// Arm the check that the first prompt after a restore is the one the save was taken on. A null
    /// argument (the save was taken between actions) disarms it.
    /// </summary>
    public void ExpectResumedPrompt(PendingPrompt prompt) => _expectedResumedPrompt = prompt;

    /// <summary>
    /// The prompts open right now, as (kind, faction) pairs. A list rather than a single value because
    /// a reaction window prompts a whole team at once and the opening discard prompts everyone.
    ///
    /// Stored in a save so that after a restore, the step that re-issues its opening prompt can be
    /// checked against the one that was actually open when the save was taken.
    /// </summary>
    public List<PendingPrompt> DescribePendingInputs()
        => _pendingInputs.Values
            .Where(pending => pending.Request != null)
            .Select(pending => new PendingPrompt
            {
                Kind = PendingPrompt.KindOf(pending.Request),
                Faction = pending.Request.TargetFaction
            })
            .ToList();

    /// <param name="withdrawToken">
    /// Cancelled by the caller when this request no longer needs an answer. Used by the reaction
    /// system: a team's turn prompts every one of its factions at once and the first card chosen ends
    /// the turn, so the prompts still open have to be taken off the other players' screens.
    ///
    /// The request comes back <see cref="InputRequest.WasSkipped"/>, exactly like a timeout Skip, so
    /// every existing caller unwinds through the path it already has. Callers that need to tell a
    /// withdrawal apart from a real pass — a withdrawn player never chose anything and must not be
    /// recorded as having passed — own the token and so already know.
    /// </param>
    public async Task<InputRequest> SendInputRequest(InputRequest inputRequest, CancellationToken withdrawToken = default)
    {
        DebugUtilities.PrintPeer($"[color={"purple"}]SendInputRequest: {inputRequest.GetType().Name}");

        // Stamp the legal move set onto the request before it goes on the wire, so it is
        // self-describing to a scripted/CLI peer. Here rather than in InputRequest.BroadCast because
        // CardPlayRound.RequestPlay and RequestBlock call this method directly, bypassing BroadCast —
        // and those build the very requests (HandCardPlay, ActivateCard) that need it. Outside the
        // retry loop: the option set must not shift between attempts at the same prompt.
        inputRequest.PopulateTargets();

        // Narrow a tutorial-constrained prompt while the option set is being minted, so the GUI, the
        // CLI and the tutorial provider all see the same offer. Here rather than in BroadCast for the
        // same reason PopulateTargets is: CardPlayRound.RequestPlay and RequestBlock come straight
        // here, and those build the very prompts a lesson most wants to constrain.
        TutorialRuntime.Current?.ConstrainRequest(inputRequest);

        // First prompt after a save restore: check the resume landed where the save was taken. Consumed
        // here rather than checked at the resume site because the resume only starts a step handler — it
        // is this call that proves the step got as far as asking the same question again.
        if (_expectedResumedPrompt != null)
        {
            PendingPrompt expected = _expectedResumedPrompt;
            _expectedResumedPrompt = null;
            GameFlow.Instance?.VerifyResumedPrompt(expected, inputRequest);
        }

        // Withdrawn before we ever reached the wire — the team turn was decided by another faction
        // while this one was still queued behind its peer's earlier prompt. Return without putting
        // anything on anyone's screen.
        if (withdrawToken.IsCancellationRequested)
        {
            inputRequest.WasSkipped = true;
            return inputRequest;
        }

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
            PendingInput pending = new()
            {
                Tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously),
                ForceTimeout = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
                Peer = SafeTargetPeer(inputRequest),
                Request = inputRequest,
            };
            string pendingId = inputRequest.Id;
            _pendingInputs[pendingId] = pending;

            TaskCompletionSource<string> tcs = pending.Tcs;
            TaskCompletionSource<bool> forceTimeout = pending.ForceTimeout;

            Rpc(nameof(NetworkApi.ReceiveInputRequest), payload);

            // Cancelled on the normal path so a long game does not accumulate one live 15-minute timer
            // per input request; the old bare Task.Delay was never cancelled.
            using CancellationTokenSource timeoutCts = new();
            // A TCS bridged off the token rather than passing it into WhenAny: the withdrawal must
            // complete this await normally, not throw OperationCanceledException through a caller that
            // has a pass path already.
            TaskCompletionSource<bool> withdrawn = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenRegistration withdrawRegistration =
                withdrawToken.Register(() => withdrawn.TrySetResult(true));

            Task completed = await Task.WhenAny(
                tcs.Task, forceTimeout.Task, withdrawn.Task, Task.Delay(InputResponseTimeoutMs, timeoutCts.Token));
            timeoutCts.Cancel();

            if (completed == tcs.Task)
            {
                ClearPendingInput(pendingId, pending);
                return InputRequest.FromJson(await tcs.Task);
            }

            if (completed == withdrawn.Task)
            {
                DebugUtilities.PrintPeer(
                    $"Withdrawing {inputRequest.GetType().Name} (Id {inputRequest.Id}) — the answer is no longer needed");

                // Same ordering as the timeout path below, for the same reason: drop the awaiter FIRST,
                // so the reply the client sends as its prompt is torn down finds no request pending and
                // is discarded as stale instead of resolving something we have already abandoned.
                //
                // Aimed at this request's own peer. A blanket abort would also close the prompts of the
                // team's other factions, which are still live and are exactly the answers we are racing
                // for. The `Peer != 0` guard keeps it that way; the no-session case falls through to
                // AbortRemoteInput's own local-release branch.
                ClearPendingInput(pendingId, pending);
                if (pending.Peer != 0 || Multiplayer?.MultiplayerPeer == null)
                    AbortRemoteInput(pending.Peer);
                AnnounceInputClosed(inputRequest.TargetFaction);

                inputRequest.WasSkipped = true;
                return inputRequest;
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
            //
            // Aimed at this request's own peer rather than broadcast: with several requests in flight
            // (the opening discard) a blanket abort would tear down the modal of every player still
            // deciding, not just the one that timed out.
            ClearPendingInput(pendingId, pending);
            AbortRemoteInput(pending.Peer);
            AnnounceInputClosed(inputRequest.TargetFaction);
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

        // A report parked for a peer that has since left would over-satisfy the next barrier:
        // PeerReadinessComponent recomputes `expected` from the live peer list, so the departed peer is
        // no longer counted, but its parked report would still be drained into _readyPeers.
        foreach (HashSet<int> parked in _bufferedBarrierReports.Values)
            parked.Remove((int)peerId);

        // Only this peer's own waits: another player may still be answering a concurrent request, and
        // releasing theirs too would throw away an answer they are about to give.
        foreach (KeyValuePair<string, PendingInput> entry in _pendingInputs)
        {
            if (entry.Value.Peer != 0 && entry.Value.Peer != (int)peerId) continue;

            DebugUtilities.PrintPeerErrorRaw(
                $"Peer {peerId} disconnected while we were waiting on its input — treating as skipped.");
            CancelPendingInput(entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// Complete every in-flight input request as skipped so the awaiting chains unwind through the
    /// existing StepSkippedException path. Called by the recovery sweep, which is tearing the whole
    /// step down — so it releases all of them, not just one.
    /// </summary>
    public void CancelPendingInputRequest()
    {
        foreach (KeyValuePair<string, PendingInput> entry in _pendingInputs)
            CancelPendingInput(entry.Key, entry.Value);
    }

    private void CancelPendingInput(string id, PendingInput pending)
    {
        TaskCompletionSource<string> tcs = pending?.Tcs;
        if (tcs == null || tcs.Task.IsCompleted) return;

        DebugUtilities.PrintPeer($"Cancelling pending input request (Id {id})");

        // Complete with the request itself, marked skipped and carrying no responses — see the note on
        // PendingInput.Request for why this rather than TrySetCanceled.
        InputRequest skipped = pending.Request;
        if (skipped != null)
        {
            skipped.WasSkipped = true;
            tcs.TrySetResult(skipped.ToJson());
            AnnounceInputClosed(skipped.TargetFaction);
        }
        else
        {
            tcs.TrySetCanceled();
        }
        ClearPendingInput(id, pending);
        GameFlow.Instance?.ClearCurrentInputRequest();
    }

    /// <summary>
    /// Drop the entry only if it is still the one <paramref name="pending"/> opened. The reference
    /// check is what makes a retry safe: attempt 2 has already replaced the entry under a new Id, so
    /// attempt 1 unwinding cannot remove it.
    /// </summary>
    private void ClearPendingInput(string id, PendingInput pending)
    {
        if (id == null || pending == null) return;
        _pendingInputs.TryRemove(new KeyValuePair<string, PendingInput>(id, pending));
    }

    /// <summary>
    /// Tell every client to release any open board selection, so a client sitting on a
    /// SelectCountry/SelectUnit prompt for an aborted step does not stay stuck on it.
    /// </summary>
    /// <param name="targetPeer">
    /// The one peer to release, or 0 for all of them. Aim it whenever only a single request is being
    /// abandoned: a broadcast also tears down the prompts of players answering a concurrent request
    /// (the opening discard asks everyone at once). The recovery sweep, which is abandoning the whole
    /// step, passes 0 on purpose.
    /// </param>
    public void AbortRemoteInput(int targetPeer = 0)
    {
        // No session at all (single process, or the peer already torn down): there is nobody to Rpc, but
        // this process may still be sitting on its own prompt, so release it directly.
        if (Multiplayer?.MultiplayerPeer == null)
        {
            AbortInputRequest();
            return;
        }
        if (!Multiplayer.IsServer()) return;
        if (targetPeer == 0)
        {
            Rpc(nameof(AbortInputRequest));
            return;
        }
        // RpcId does not call locally even for the host's own id, so the host releases its own prompt
        // by hand — the same asymmetry CallLocal covers on the broadcast path.
        if (targetPeer == Multiplayer.GetUniqueId()) AbortInputRequest();
        else RpcId(targetPeer, nameof(AbortInputRequest));
    }

    /// <summary>Server → all peers: cancel any local board selection currently awaiting a click.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void AbortInputRequest()
    {
        PendingLocalInput.CancelAll();
        // The whole stack, not one modal: a prompt the player parked is invisible but still pending, and
        // an info modal may be open beside or over it.
        ModalStack.Current?.CancelAll();

        // Covers the peers that were only watching: their Execute() took the "Waiting on X" branch and
        // returned, so nothing local will ever clear their countdown.
        InputTimerDisplay.Current?.Hide();
    }

    /// <summary>
    /// Server → all peers: one pending input has closed — answered, withdrawn, or given up on.
    ///
    /// Needed because the answering peer's <c>ReceiveInputResponse</c> does not reach the other clients —
    /// an AnyPeer Rpc from a client only reaches the server (see <see cref="ReportErrorToServer"/> for the
    /// same asymmetry). Without this, every peer that was merely watching would count its countdown down
    /// to 0:00 and leave it stranded there while play carried on.
    ///
    /// Carries the faction rather than being the bare "everything is answered" signal it used to be: a
    /// reaction window takes a whole team's turn at once, so several prompts are open together and each
    /// watcher has to know which one closed. The "is anything still open" test that used to live here as
    /// a host-side <c>_pendingInputs.IsEmpty</c> gate now reads the receiver's own waiting set, which is
    /// the same condition answered locally.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void InputRequestAnswered(int faction)
    {
        InputRequest.MarkInputClosed((Faction)faction);
    }

    /// <summary>Host-side helper: tell every peer that this faction's prompt is no longer open.</summary>
    private void AnnounceInputClosed(Faction faction)
    {
        if (Multiplayer?.MultiplayerPeer != null) Rpc(nameof(InputRequestAnswered), (int)faction);
        else InputRequestAnswered((int)faction);   // single process: no wire, but the label is still ours
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
            // from an aborted step cannot resume a dead continuation. Looking the id up in
            // _pendingInputs covers both of the old guards at once: "nothing pending" and "pending,
            // but a different request" are the same miss, because each attempt is keyed by its own
            // fresh Guid.
            if (response?.Id == null || !_pendingInputs.TryRemove(response.Id, out PendingInput pending))
            {
                DebugUtilities.PrintPeer(
                    $"Ignoring stale input response (Id {response?.Id}) — no matching request pending");
                return;
            }

            // Assigned only once the response is known to be the one we are waiting on. It used to be
            // set above the guards, so a late reply to a timed-out request still overwrote the slot.
            // Still a single slot with several requests in flight: it is diagnostic context for the
            // timeout popup, so last-answered-wins is fine.
            GameFlow.Instance.CurrentInputRequest = response;

            // Drop this faction from every peer's waiting list, not just here. Below the guards on
            // purpose: announcing a stale reply would drop a faction whose retry is already in progress.
            // Each peer hides its own countdown once its list empties, so the first player to answer a
            // concurrent team turn still does not blank the countdown of everyone else choosing.
            AnnounceInputClosed(pending.Request?.TargetFaction ?? response.TargetFaction);

            pending.Tcs.TrySetResult(dtoJson);

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
