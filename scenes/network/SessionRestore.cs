using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Rebuilds a saved game on every peer. A child of <see cref="MultiplayerSession"/>, so it exists at the
/// same path on host and clients and can carry the restore RPCs.
///
/// The host replays the save's event log locally, then sends clients that log in paced chunks; each
/// client applies it through the normal replicated-message path and reports its final hash. The host
/// resumes the turn loop only once every client has. Capturing a save is <see cref="SaveCapture"/>.
/// </summary>
public partial class SessionRestore : Node
{
    private PeerReadinessComponent _restoreReadiness => GetNode<PeerReadinessComponent>("RestoreReadinessComponent");

    private static MultiplayerGameState GameState => MultiplayerSession.Instance.GameState;

    /// <summary>
    /// How many replayed events to apply between frame yields. Small enough that the cover keeps
    /// painting, large enough that a long game does not take a frame per event.
    /// </summary>
    private const int ReplayYieldInterval = 25;

    /// <summary>
    /// Upper bound on one restore chunk's JSON. Steam refuses a single message over 512 KB and its
    /// default send buffer is the same size, so a whole late-game log cannot go in one message.
    /// </summary>
    private const int RestoreChunkBytes = 32 * 1024;

    /// <summary>
    /// Pause between chunks. A per-event broadcast during the replay sent ~1000 reliable messages in a
    /// second; Steam's send buffer overflowed and dropped some without any error on either side.
    /// </summary>
    private const double RestoreChunkSpacingSeconds = 0.1;

    /// <summary>The client's expected hash once the whole log is applied; null outside a restore.</summary>
    private string _expectedRestoreHash;

    /// <summary>Client: restore events queued so far, for the progress line and the final log.</summary>
    private int _restoreEventsQueued;

    // ── Host ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Host-only: rebuild a saved game by REPLAYING its ChangeEvent log onto the freshly-built shell.
    ///
    /// Replaying runs the real pipeline with animations suppressed, so the board, decks, victory points,
    /// modifiers, card history and the history panel all reconstruct through the live code rather than
    /// through a parallel deserializer. Clients are not sent each event as it applies: they get the
    /// whole log afterwards (see SendRestoreLog) and apply it the same way. Each event's DTO
    /// carries the outcome it produced the first time, so this is re-applying decisions rather than
    /// re-making them, and it does not depend on the game being deterministic.
    ///
    /// The caller resumes the turn machine; this method only rebuilds state.
    /// </summary>
    public async Task RestoreAsync(GameModeMultiplayerDefault gameMode, SaveGame save)
    {
        ReplayContext.BeginFastForward();
        Rpc(nameof(BeginFastForward));

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

        // Before the CalculateAll below: its tag snapshot is the first broadcast since the replay began,
        // and it must reach clients after the events it describes, not before them.
        // The replay broadcast nothing, so the journal a rejoining player is caught up from starts here.
        BroadcastJournal.Seed(save.Events);
        await SendRestoreLog(save.Events, GameState.ComputeHash());

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

    /// <summary>Host: completes once every peer has applied the restore log (or has left).</summary>
    public async Task WhenAllPeersRestored()
    {
        var done = new TaskCompletionSource();
        void OnReady() => done.TrySetResult();
        _restoreReadiness.AllPeersReady += OnReady;
        try
        {
            _restoreReadiness.RegisterReady();
            await done.Task;
        }
        finally
        {
            _restoreReadiness.AllPeersReady -= OnReady;
        }
    }

    /// <summary>
    /// Host-only: send clients the save's event log in paced chunks, with the hash the host's own replay
    /// reached. Each event still carries its recorded hash, so a client verifies every one of them.
    /// </summary>
    /// <param name="targetPeer">One peer to send to (a player rejoining), or 0 for every client.</param>
    public async Task SendRestoreLog(List<GameMessageDto> events, string finalHash, int targetPeer = 0)
    {
        if (Multiplayer.GetPeers().Length == 0) return;

        if (targetPeer != 0) RpcId(targetPeer, nameof(BeginFastForward));

        List<string> chunks = ChunkEvents(events);
        DebugUtilities.PrintPeer($"SendRestoreLog: {events.Count} event(s) in {chunks.Count} chunk(s), final hash {finalHash}");
        for (int i = 0; i < chunks.Count; i++)
        {
            if (i > 0)
                await ToSignal(GetTree().CreateTimer(RestoreChunkSpacingSeconds), SceneTreeTimer.SignalName.Timeout);
            if (targetPeer != 0)
                RpcId(targetPeer, nameof(ReceiveRestoreLog), i, chunks.Count, events.Count, chunks[i], finalHash);
            else
                Rpc(nameof(ReceiveRestoreLog), i, chunks.Count, events.Count, chunks[i], finalHash);
        }
    }

    /// <summary>Split the log into JSON arrays of whole events, each under RestoreChunkBytes.</summary>
    private static List<string> ChunkEvents(List<GameMessageDto> events)
    {
        var chunks  = new List<string>();
        var current = new List<string>();
        int size    = 0;
        foreach (GameMessageDto dto in events)
        {
            // Against GameMessageDto explicitly, as GameMessage.BroadCast does, so "$type" is written.
            string json = JsonSerializer.Serialize(dto, typeof(GameMessageDto));
            if (current.Count > 0 && size + json.Length > RestoreChunkBytes)
            {
                chunks.Add($"[{string.Join(",", current)}]");
                current.Clear();
                size = 0;
            }
            current.Add(json);
            size += json.Length + 1;
        }
        if (current.Count > 0)
            chunks.Add($"[{string.Join(",", current)}]");
        return chunks;
    }

    // ── Client ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Tell every client to stop animating: the host is about to replay a saved game and send its whole
    /// event log. CallLocal = false because the host sets its own flag directly; a reliable Rpc sent
    /// before the log is ordered ahead of it.
    ///
    /// Cleared again in MultiplayerSession.HideLoadingScreenWhenReady, NOT on a queue drain —
    /// ChangeEventQueue.WhenDrained means "the queue is empty right now", which can happen mid-restore.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void BeginFastForward() => ReplayContext.BeginFastForward();

    /// <summary>
    /// Client: queue one chunk of the restore log. Enqueued synchronously, so chunks stay in wire order
    /// with each other and ahead of everything the host sends after them.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveRestoreLog(int chunkIndex, int chunkCount, int totalEvents, string eventsJson, string finalHash)
    {
        try
        {
            ReplayContext.IsApplyingRestoreLog = true;
            _expectedRestoreHash = finalHash;

            List<GameMessageDto> dtos = JsonSerializer.Deserialize<List<GameMessageDto>>(eventsJson);
            DebugUtilities.PrintPeer($"ReceiveRestoreLog: chunk {chunkIndex + 1}/{chunkCount}, {dtos.Count} event(s)");
            foreach (GameMessageDto dto in dtos)
            {
                RejoinService.Instance?.NoteLogged(dto.Id);
                GameMessage msg = NetworkApi.Instance.EnqueueReplicated(dto, "RestoreLog");
                if (msg is ChangeEvent ev && ++_restoreEventsQueued % ReplayYieldInterval == 0)
                {
                    int queued = _restoreEventsQueued;
                    ev.ChangeEventApplied += _ => ReportRestoreProgress(queued, totalEvents);
                }
            }
        }
        catch (Exception e)
        {
            ErrorReporter.Report(e, "Rpc ReceiveRestoreLog");
        }
    }

    /// <summary>
    /// Client: the restore log is applied. Check the final hash, return to normal rule checks, and tell
    /// the host this peer may resume. Always reports, so a failed restore cannot hang the host.
    /// </summary>
    public void FinishClientRestore()
    {
        ReplayContext.IsApplyingRestoreLog = false;
        string hash = GameState.ComputeHash();
        if (_expectedRestoreHash != null && hash != _expectedRestoreHash)
            DebugUtilities.PrintPeerErrorRaw(
                $"Restore: client board diverged from the host — expected hash {_expectedRestoreHash}, got {hash}.");
        else
            DebugUtilities.PrintPeer($"Restore: applied {_restoreEventsQueued} event(s), hash {hash} matches the host.");

        _expectedRestoreHash = null;
        _restoreEventsQueued = 0;

        // A rejoin is caught up on its own, not by the start-of-game barrier everyone else passed.
        if (RejoinService.Instance?.IsCatchingUp == true) RejoinService.Instance.OnCaughtUp();
        else _restoreReadiness.RegisterReady();
    }

    /// <summary>Line under the loading cover. Local only: each peer reports its own replay's progress.</summary>
    private static void ReportRestoreProgress(int applied, int total)
        => GameManager.Instance?.SetLoadingStatus($"Restoring… {applied} / {total}");
}
