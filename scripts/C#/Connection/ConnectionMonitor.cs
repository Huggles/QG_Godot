using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Watches every connection in a running game with its own ping, so "unreachable" means the same
/// thing on ENet and Steam and a short blip heals without anyone rejoining.
///
/// The host pings each player once a second; a player silent for <see cref="UnreachableAfter"/>
/// pauses the game (<see cref="GamePause"/>) and, at <see cref="DisconnectAfter"/>, the host decides:
/// wait, save and quit, or let a bot take over. Clients watch the host's pings the same way and are
/// told when it is gone. Transport timeouts are raised above that window so the transport never
/// gives up first.
///
/// An autoload, like NetworkApi, so its RPC path exists on every peer from the first frame. Seats are
/// keyed by their registry id (the player's original peer id), never by connection.
/// </summary>
public partial class ConnectionMonitor : Node
{
    public static ConnectionMonitor Instance { get; private set; }

    private const double PingInterval = 1.0;
    private const double TableInterval = 2.0;
    private const double UnstableAfter = 3.0;
    private const double UnreachableAfter = 5.0;
    public const double DisconnectAfter = 30.0;

    /// <summary>Above our own window, so the transport never drops a connection we are still waiting on.</summary>
    private const int TransportTimeoutMinMs = 35000;
    private const int TransportTimeoutMaxMs = 40000;

    private const int PingChannel = 1;
    private const int TableChannel = 2;

    /// <summary>Raised on every peer when a new player table arrives.</summary>
    public event Action TableChanged;

    /// <summary>The latest player table, one row per person; empty outside a multiplayer game.</summary>
    public IReadOnlyList<PlayerConnectionRow> Rows => _rows;
    private List<PlayerConnectionRow> _rows = new();

    private sealed class Seat
    {
        public int Id;
        public double LastHeard;
        public double RttMs;
        public bool Gone;
        public SeatState State = SeatState.Connected;
        /// <summary>The host chose to wait for this absence; do not ask again until it ends.</summary>
        public bool Decided;
    }

    private readonly Dictionary<int, Seat> _seats = new();
    private bool _armed;
    private double _tickTimer;
    private double _tableTimer;
    private double _lastHeardHost;
    private bool _hostLost;
    private ConnectionLostDialog _dialog;
    private ConnectionBanner _banner;

    private static double Now => Time.GetTicksMsec() / 1000.0;

    private bool IsHost => Multiplayer?.MultiplayerPeer != null && Multiplayer.IsServer();

    public override void _Ready()
    {
        Instance = this;
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
        if (Multiplayer == null) return;
        Multiplayer.PeerConnected -= OnPeerConnected;
        Multiplayer.PeerDisconnected -= OnPeerDisconnected;
        Multiplayer.ServerDisconnected -= OnServerDisconnected;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Start watching. Called on every peer once its loading cover lifts. Seats come from the players
    /// dealt factions, so one already gone at this point counts as absent from the start.
    /// </summary>
    public void Arm()
    {
        if (Multiplayer?.MultiplayerPeer is null or OfflineMultiplayerPeer) return;
        // Headless and CLI runs have nobody to answer the host's decision, and a slow peer there is not a lost one.
        if (GameContext.IsHeadless) return;

        _armed = true;
        _hostLost = false;
        _lastHeardHost = Now;
        _seats.Clear();
        if (!IsHost) return;

        int[] connected = Multiplayer.GetPeers();
        foreach (int id in PlayerFactionRegistry.GetHumanSeatIds().Where(id => id != PlayerFactionRegistry.HostPeerId))
            _seats[id] = new Seat { Id = id, LastHeard = Now, Gone = !connected.Contains(id) };

        HostTick();
    }

    /// <summary>Session teardown (SceneFlow): stop watching and let go of everything shown.</summary>
    public void Reset()
    {
        _armed = false;
        _seats.Clear();
        _rows = new List<PlayerConnectionRow>();
        GamePause.Reset();
        _dialog?.Dismiss();
        _dialog = null;
        if (IsInstanceValid(_banner)) _banner.QueueFree();
        _banner = null;
        TableChanged?.Invoke();
    }

    /// <summary>Whether the host is holding the game for this seat.</summary>
    public bool IsSeatAbsent(int seatId) => _seats.TryGetValue(seatId, out Seat seat) && seat.State == SeatState.Absent;

    public override void _Process(double delta)
    {
        if (!_armed) return;
        _tickTimer += delta;
        if (_tickTimer < PingInterval) return;
        _tickTimer = 0;

        if (IsHost) HostTick();
        else ClientTick();
    }

    // ── Ping ─────────────────────────────────────────────────────────────────

    /// <summary>Unreliable and on its own channel, so it never waits behind game traffic.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
        TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable, TransferChannel = PingChannel)]
    public void Ping(long sentAtMs)
    {
        _lastHeardHost = Now;
        RpcId(1, MethodName.Pong, sentAtMs);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
        TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable, TransferChannel = PingChannel)]
    public void Pong(long sentAtMs)
    {
        if (!IsHost || !_seats.TryGetValue(Multiplayer.GetRemoteSenderId(), out Seat seat) || seat.Gone) return;

        seat.LastHeard = Now;
        double rtt = Math.Max(0, (long)Time.GetTicksMsec() - sentAtMs);
        seat.RttMs = seat.RttMs <= 0 ? rtt : seat.RttMs * 0.7 + rtt * 0.3;
    }

    // ── Host ─────────────────────────────────────────────────────────────────

    private void HostTick()
    {
        int[] connected = Multiplayer.GetPeers();
        long nowMs = (long)Time.GetTicksMsec();
        foreach (Seat seat in _seats.Values)
            if (seat.State != SeatState.Bot && !seat.Gone && connected.Contains(seat.Id))
                RpcId(seat.Id, MethodName.Ping, nowMs);

        bool changed = UpdateSeatStates();

        _tableTimer += PingInterval;
        bool anyAbsent = _seats.Values.Any(s => s.State == SeatState.Absent);
        if (changed || anyAbsent || _tableTimer >= TableInterval)
        {
            _tableTimer = 0;
            Rpc(MethodName.ReceiveTable, JsonSerializer.Serialize(BuildTable()));
        }

        if (anyAbsent) GamePause.Pause();
        else GamePause.Resume();

        if (_dialog == null && _seats.Values.Any(NeedsDecision))
            Guard.FireAndForget(AskHostAsync, "ConnectionMonitor.AskHost");
    }

    private bool UpdateSeatStates()
    {
        bool changed = false;
        double now = Now;
        foreach (Seat seat in _seats.Values)
        {
            if (seat.State == SeatState.Bot) continue;

            double silence = now - seat.LastHeard;
            SeatState state = seat.Gone || silence > UnreachableAfter ? SeatState.Absent
                : silence > UnstableAfter ? SeatState.Unstable
                : SeatState.Connected;

            if (state == seat.State) continue;
            changed = true;
            seat.State = state;
            if (state != SeatState.Absent) seat.Decided = false;
            DebugUtilities.PrintPeer($"ConnectionMonitor: seat {seat.Id} is now {state}");
        }

        // Everyone the host was asking about came back: the question no longer stands.
        if (_dialog != null && !_seats.Values.Any(s => s.State == SeatState.Absent))
        {
            _dialog.Dismiss();
            _dialog = null;
        }
        return changed;
    }

    private bool NeedsDecision(Seat seat) =>
        seat.State == SeatState.Absent && !seat.Decided && Now - seat.LastHeard >= DisconnectAfter;

    private List<PlayerConnectionRow> BuildTable()
    {
        List<PlayerConnectionRow> rows = new();
        if (PlayerFactionRegistry.GetFactionsForPeerId(PlayerFactionRegistry.HostPeerId).Count > 0)
            rows.Add(new PlayerConnectionRow(PlayerFactionRegistry.HostPeerId,
                PlayerFactionRegistry.GetDisplayNameForPeer(PlayerFactionRegistry.HostPeerId), 0,
                SeatState.Connected, 0, IsHost: true));

        double now = Now;
        foreach (Seat seat in _seats.Values.OrderBy(s => s.Id))
        {
            int left = (int)Math.Ceiling(Math.Max(0, DisconnectAfter - (now - seat.LastHeard)));
            rows.Add(new PlayerConnectionRow(seat.Id, PlayerFactionRegistry.GetDisplayNameForPeer(seat.Id),
                (int)Math.Round(seat.RttMs), seat.State, seat.State == SeatState.Absent ? left : 0, IsHost: false));
        }
        return rows;
    }

    /// <summary>
    /// The host's choice for every player past the window. Re-checked after the dialog closes: a player
    /// who came back while it was open needs nothing done.
    /// </summary>
    private async Task AskHostAsync()
    {
        List<Seat> lost = _seats.Values.Where(NeedsDecision).ToList();
        if (lost.Count == 0) return;

        string names = string.Join(", ", lost.Select(s => PlayerFactionRegistry.GetDisplayNameForPeer(s.Id)));
        bool canSave = GameFlow.Instance?.CanSave == true;

        Task<ConnectionLostDialog.Choice> prompt =
            ConnectionLostDialog.PromptHostAsync(GetTree().CurrentScene, names, canSave, out _dialog);
        ConnectionLostDialog.Choice choice = await prompt;
        _dialog = null;
        if (!_armed) return;

        lost = lost.Where(s => s.State == SeatState.Absent).ToList();
        switch (choice)
        {
            case ConnectionLostDialog.Choice.SaveAndQuit:
                if (canSave && GameFlow.Instance?.CanSave == true) SaveCapture.Capture(AutoSaveName());
                SceneFlow.ChangeScene(GetTree().CurrentScene, "res://scenes/menu/Menu.tscn", leaveSession: true);
                break;
            case ConnectionLostDialog.Choice.Bot:
                foreach (Seat seat in lost) TakeOverWithBot(seat);
                HostTick();
                break;
            default:
                foreach (Seat seat in lost) seat.Decided = true;
                break;
        }
    }

    /// <summary>Host: reopen the choice for everyone it was put off for (the banner's Options button).</summary>
    public void ReopenDecision()
    {
        if (!IsHost || _dialog != null) return;
        foreach (Seat seat in _seats.Values.Where(s => s.State == SeatState.Absent)) seat.Decided = false;
        Guard.FireAndForget(AskHostAsync, "ConnectionMonitor.ReopenDecision");
    }

    private void TakeOverWithBot(Seat seat)
    {
        Rpc(MethodName.SetSeatAi, seat.Id, true);
        AiSeatRuntime.TakeOverSeat(PlayerFactionRegistry.GetPlayerSceneForSeat(seat.Id));
        seat.State = SeatState.Bot;
        NetworkApi.Instance?.RerunInputsForPeer(seat.Id);
    }

    /// <summary>Every peer flips the seat in its own registry, so labels and "Waiting on" agree.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SetSeatAi(int seatId, bool isAi) => PlayerFactionRegistry.MarkSeatAi(seatId, isAi);

    private static string AutoSaveName()
    {
        string scenario = GameManager.ActiveScenarioTitle ?? "Game";
        return $"{scenario} — Round {GameFlow.Instance?.Round}, {GameFlow.Instance?.CurrentFaction.Label()} (connection lost)";
    }

    // ── Table (every peer) ───────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
        TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = TableChannel)]
    public void ReceiveTable(string json)
    {
        if (!_armed) return;
        _rows = JsonSerializer.Deserialize<List<PlayerConnectionRow>>(json) ?? new List<PlayerConnectionRow>();
        TableChanged?.Invoke();
        RefreshBanner();
    }

    private void RefreshBanner()
    {
        List<PlayerConnectionRow> absent = _rows.Where(r => r.State == SeatState.Absent).ToList();
        if (absent.Count == 0)
        {
            Banner?.Hide();
            return;
        }

        string names = string.Join(", ", absent.Select(r => r.Name));
        int left = absent.Max(r => r.SecondsLeft);
        string text = left > 0
            ? $"Waiting for {names} to reconnect… {left}s"
            : $"Waiting for {names} to reconnect…";

        bool offerOptions = IsHost && _dialog == null && left == 0;
        EnsureBanner().Show(text, offerOptions);
    }

    // ── Client ───────────────────────────────────────────────────────────────

    private void ClientTick()
    {
        if (_hostLost) return;
        double silence = Now - _lastHeardHost;

        if (silence >= DisconnectAfter)
        {
            ShowHostLost();
            return;
        }

        if (silence > UnreachableAfter)
            EnsureBanner().Show($"Connection to the host lost… {(int)Math.Ceiling(DisconnectAfter - silence)}s", false);
        else if (!_rows.Any(r => r.State == SeatState.Absent))
            Banner?.Hide();
    }

    private void OnServerDisconnected()
    {
        if (_armed) ShowHostLost();
    }

    /// <summary>The host is gone: the only way on is out. Clients never get to save a networked game.</summary>
    private void ShowHostLost()
    {
        if (_hostLost) return;
        _hostLost = true;
        Banner?.Hide();
        Guard.FireAndForget(async () =>
        {
            await ConnectionLostDialog.PromptHostLostAsync(GetTree().CurrentScene);
            SceneFlow.ChangeScene(GetTree().CurrentScene, "res://scenes/menu/Menu.tscn", leaveSession: true);
        }, "ConnectionMonitor.HostLost");
    }

    // ── Transport ────────────────────────────────────────────────────────────

    private void OnPeerConnected(long id)
    {
        // A client only has a direct ENet link to the host; asking for any other peer logs an error.
        if (!IsHost && id != 1) return;
        if (Multiplayer.MultiplayerPeer is ENetMultiplayerPeer enet)
            enet.GetPeer((int)id)?.SetTimeout(32, TransportTimeoutMinMs, TransportTimeoutMaxMs);
    }

    private void OnPeerDisconnected(long id)
    {
        if (!_armed || !IsHost || !_seats.TryGetValue((int)id, out Seat seat)) return;
        seat.Gone = true;
        HostTick();
    }

    /// <summary>Steam's equivalent of the ENet timeout above; called once Steam is up.</summary>
    public static int SteamTimeoutMs => TransportTimeoutMaxMs;

    // ── UI ───────────────────────────────────────────────────────────────────

    private ConnectionBanner Banner => IsInstanceValid(_banner) ? _banner : null;

    private ConnectionBanner EnsureBanner()
    {
        if (Banner != null) return _banner;
        _banner = new ConnectionBanner();
        _banner.OptionsPressed += ReopenDecision;
        AddChild(_banner);
        return _banner;
    }
}
