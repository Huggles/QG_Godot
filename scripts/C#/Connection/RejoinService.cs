using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Lets a player who lost their connection back into the game they were in.
///
/// Every joining peer first says <see cref="Hello"/> with its <see cref="GameSettings.PlayerToken"/>.
/// The host answers: go to the lobby, rejoin as seat N, or go away (a game is in progress). A rejoin
/// keeps the seat's original id — PlayerScene nodes, the registry and prompt routing never change — and
/// the host maps that seat to the player's new connection for anything it sends them.
///
/// Catching up reuses the save-restore pipeline: the client builds a blank game exactly as at game
/// start, the host sends it every ChangeEvent so far (<see cref="BroadcastJournal"/>), and the seat's
/// open prompt is re-sent once it reports in. The game is paused for the whole of it. Live messages
/// that arrive meanwhile are held and applied afterwards, minus the ones the log already had.
///
/// An autoload, so its RPC path exists before either side has a game scene.
/// </summary>
public partial class RejoinService : Node
{
    public static RejoinService Instance { get; private set; }

    public enum HelloResult { Lobby, Rejoin, Refused, NoAnswer }

    private const int HelloTimeoutMs = 10000;
    private const double StrangerGraceSeconds = 15.0;

    // ── Host state ───────────────────────────────────────────────────────────
    private readonly Dictionary<int, string> _lobbyTokens = new();      // transport id → token, in the lobby
    private readonly Dictionary<int, string> _seatTokens = new();       // seat id → token, from game start
    private readonly Dictionary<int, int> _transportBySeat = new();     // only seats that have rejoined
    private readonly HashSet<int> _rejoining = new();                    // transports mid-catch-up

    // ── Client state ─────────────────────────────────────────────────────────
    private TaskCompletionSource<(HelloResult, string)> _hello;
    private readonly List<string> _buffered = new();
    private readonly HashSet<int> _logged = new();

    /// <summary>Client: this peer is being caught up, so live game messages are held, not applied.</summary>
    public bool IsCatchingUp { get; private set; }

    private bool IsHost => Multiplayer?.MultiplayerPeer != null && Multiplayer.IsServer();

    public override void _Ready()
    {
        Instance = this;
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
        if (Multiplayer == null) return;
        Multiplayer.PeerConnected -= OnPeerConnected;
        Multiplayer.PeerDisconnected -= OnPeerDisconnected;
    }

    /// <summary>Session teardown (SceneFlow).</summary>
    public void Reset()
    {
        _lobbyTokens.Clear();
        _seatTokens.Clear();
        _transportBySeat.Clear();
        _rejoining.Clear();
        _buffered.Clear();
        _logged.Clear();
        IsCatchingUp = false;
    }

    // ── Seat ↔ connection ────────────────────────────────────────────────────

    /// <summary>Host: the connection a seat is reached on now. The seat's own id until it rejoins.</summary>
    public static int ToTransport(int seatId)
        => Instance != null && Instance._transportBySeat.TryGetValue(seatId, out int transport) ? transport : seatId;

    /// <summary>Host: the seat a connection plays, or -1 for a seat's old, replaced connection.</summary>
    public static int ToSeat(int transportId)
    {
        if (Instance == null) return transportId;
        foreach ((int seat, int transport) in Instance._transportBySeat)
            if (transport == transportId) return seat;
        return Instance._transportBySeat.ContainsKey(transportId) ? -1 : transportId;
    }

    /// <summary>
    /// Host: whether game traffic (chat) may go to this connection — anyone in the lobby, but in a game
    /// only the players' live connections, never a peer still waiting at a join screen.
    /// </summary>
    public static bool IsAdmitted(int transportId)
    {
        if (Instance == null || MultiplayerSession.Instance == null) return true;
        int seat = ToSeat(transportId);
        return seat > 0 && !Instance._rejoining.Contains(transportId)
               && PlayerFactionRegistry.GetPlayerSceneForSeat(seat) != null;
    }

    /// <summary>Host, at game start: remember whose token each seat is.</summary>
    public void RecordSeatTokens(IEnumerable<int> seatIds)
    {
        _seatTokens.Clear();
        foreach (int seat in seatIds)
            if (_lobbyTokens.TryGetValue(seat, out string token)) _seatTokens[seat] = token;
    }

    // ── Hello ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Client, just connected: ask the host where to go. Answers Rejoin when this peer has been sent
    /// back into the running game — the caller then has nothing left to do.
    /// </summary>
    public async Task<(HelloResult Result, string Message)> HelloAsync()
    {
        _hello = new TaskCompletionSource<(HelloResult, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
        RpcId(1, MethodName.Hello, GameSettings.Instance.PlayerToken);

        Task timeout = Task.Delay(HelloTimeoutMs);
        if (await Task.WhenAny(_hello.Task, timeout) == timeout)
            return (HelloResult.NoAnswer, "The host did not answer.");
        return await _hello.Task;
    }

    /// <summary>Client, from the lobby: make sure the host has our token, for a rejoin later on.</summary>
    public void AnnounceToken() => RpcId(1, MethodName.Hello, GameSettings.Instance.PlayerToken);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void Hello(string token)
    {
        if (!IsHost) return;
        int sender = Multiplayer.GetRemoteSenderId();

        if (MultiplayerSession.Instance == null)
        {
            _lobbyTokens[sender] = token;
            RpcId(sender, MethodName.HelloReply, (int)HelloResult.Lobby, "");
            return;
        }

        if (ConnectionMonitor.Instance?.IsArmed != true)
        {
            Refuse(sender, "The game is still starting. Try again in a moment.");
            return;
        }

        int seat = _seatTokens
            .Where(kv => kv.Value == token && ConnectionMonitor.Instance.CanRejoin(kv.Key))
            .Select(kv => kv.Key)
            .DefaultIfEmpty(-1)
            .First();
        if (seat < 0)
        {
            Refuse(sender, "That game is already in progress.");
            return;
        }

        AcceptRejoin(sender, seat);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void HelloReply(int result, string message) => _hello?.TrySetResult(((HelloResult)result, message));

    private void Refuse(int peer, string reason)
    {
        RpcId(peer, MethodName.HelloReply, (int)HelloResult.Refused, reason);
        DropLater(peer, 1.0);
    }

    // ── Host: rejoin ─────────────────────────────────────────────────────────

    private void AcceptRejoin(int transport, int seat)
    {
        int old = ToTransport(seat);
        _transportBySeat[seat] = transport;
        _rejoining.Add(transport);
        DebugUtilities.PrintPeer($"RejoinService: seat {seat} is rejoining on connection {transport}");

        // The old connection may still be lingering inside the transport timeout; it is dead to us now.
        if (old != transport && Multiplayer.GetPeers().Contains(old))
            Multiplayer.MultiplayerPeer.DisconnectPeer(old);

        ConnectionMonitor.Instance.BeginRejoin(seat);
        RpcId(transport, MethodName.BeginRejoin, seat, JsonSerializer.Serialize(CurrentAssignments()), GameRandom.Seed);
    }

    /// <summary>The seating as it is now, bots included, in the shape LoadPlayers reads.</summary>
    private static List<PlayerFactionAssignment> CurrentAssignments()
        => PlayerFactionRegistry.GetAllPlayers()
            .Select(scene => new PlayerFactionAssignment(
                scene.GetMultiplayerAuthority(),
                PlayerFactionRegistry.GetFactionsForPeerId(scene.GetMultiplayerAuthority()),
                scene.DisplayName,
                scene.IsAiSeat))
            .ToList();

    /// <summary>Client has a blank game up and is holding live messages: send it everything so far.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RejoinReady()
    {
        int sender = Multiplayer.GetRemoteSenderId();
        if (!IsHost || !_rejoining.Contains(sender)) return;

        Guard.FireAndForget(async () =>
        {
            List<GameMessageDto> events = BroadcastJournal.Snapshot();
            // No final hash: an event can be mutated on the host before it is broadcast, so the host's
            // current hash may be ahead of the log. Every event in it still carries its own hash.
            await MultiplayerSession.Instance.Restore.SendRestoreLog(events, null, sender);
            MultiplayerSession.Instance.RpcId(sender, nameof(MultiplayerSession.HideLoadingScreenWhenReady), true);
        }, "RejoinService.SendLog");
    }

    /// <summary>Client is caught up: give it the tags and turn position, hand the seat back, resume.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RejoinComplete()
    {
        int sender = Multiplayer.GetRemoteSenderId();
        if (!IsHost || !_rejoining.Remove(sender)) return;
        int seat = ToSeat(sender);

        new RecalculateTagsMessage(GameStateCalculator.BuildTagsSnapshot()).SendTo(sender);
        GameFlow flow = GameFlow.Instance;
        if (flow != null)
            RpcId(sender, MethodName.SyncFlow, flow.IsStarted, flow.GameTurn, flow.TurnStepCounter, (int)flow.TurnStep);

        DebugUtilities.PrintPeer($"RejoinService: seat {seat} is back");
        ConnectionMonitor.Instance?.CompleteRejoin(seat);
        NetworkApi.Instance?.RerunInputsForPeer(seat);
    }

    private void OnPeerConnected(long id)
    {
        // In a game, a connection must identify itself as a returning player promptly or it is let go.
        if (IsHost && MultiplayerSession.Instance != null) DropLater((int)id, StrangerGraceSeconds, unlessAdmitted: true);
    }

    private void OnPeerDisconnected(long id)
    {
        if (!IsHost || !_rejoining.Remove((int)id)) return;
        int seat = ToSeat((int)id);
        if (seat > 0) ConnectionMonitor.Instance?.AbortRejoin(seat);
    }

    private void DropLater(int peer, double seconds, bool unlessAdmitted = false)
    {
        GetTree().CreateTimer(seconds).Timeout += () =>
        {
            if (!IsHost || !Multiplayer.GetPeers().Contains(peer)) return;
            if (unlessAdmitted && (_rejoining.Contains(peer) || ToSeat(peer) > 0 && IsAdmitted(peer))) return;
            DebugUtilities.PrintPeer($"RejoinService: dropping connection {peer}, which is not a player in this game");
            Multiplayer.MultiplayerPeer.DisconnectPeer(peer);
        };
    }

    // ── Client: rejoin ───────────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void BeginRejoin(int seatId, string configuration, int seed)
    {
        _hello?.TrySetResult((HelloResult.Rejoin, ""));
        Guard.FireAndForget(() => RejoinAsync(seatId, configuration, seed), "RejoinService.Rejoin");
    }

    /// <summary>The client half of game start, run locally: scene, session, players, blank board, HUD.</summary>
    private async Task RejoinAsync(int seatId, string configuration, int seed)
    {
        SessionIdentity.AdoptSeatId(seatId);
        IsCatchingUp = true;
        _buffered.Clear();
        _logged.Clear();

        SceneFlow.ChangeScene(GetTree().CurrentScene, SceneFlow.GameScenePath);
        while (GetNodeOrNull("/root/Game/Players") == null)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        NetworkApi.Instance.LoadMultiplayerSession();
        NetworkApi.Instance.LoadPlayers(configuration);

        SignalAwaiter started = ToSignal(EventBus.Instance, EventBus.SignalName.GameSessionStarted);
        MultiplayerSession.Instance.StartSession(configuration, seed);
        await started;

        RpcId(1, MethodName.RejoinReady);
    }

    /// <summary>Client: hold a live message while catching up. False when it should be applied as usual.</summary>
    public bool BufferIfCatchingUp(string dtoJson)
    {
        if (!IsCatchingUp) return false;
        _buffered.Add(dtoJson);
        return true;
    }

    /// <summary>Client: an event arrived in the catch-up log, so its live copy (if held) is a duplicate.</summary>
    public void NoteLogged(int id)
    {
        if (IsCatchingUp) _logged.Add(id);
    }

    /// <summary>
    /// Client: the log is applied. Apply the held live events it did not already contain — never
    /// presentation (stale by now) or tags (fresh ones follow) — then report in.
    /// </summary>
    public void OnCaughtUp()
    {
        IsCatchingUp = false;
        foreach (string json in _buffered)
        {
            GameMessageDto dto = JsonSerializer.Deserialize<GameMessageDto>(json);
            if (dto is PresentationEventDto or RecalculateTagsMessageDto || _logged.Contains(dto.Id)) continue;
            NetworkApi.Instance.EnqueueReplicated(dto, "RejoinBuffer");
        }
        _buffered.Clear();
        _logged.Clear();
        RpcId(1, MethodName.RejoinComplete);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncFlow(bool started, int turn, int counter, int step)
        => GameFlow.Instance?.ApplyReplicatedFlow(started, turn, counter, (TurnStep)step);
}
