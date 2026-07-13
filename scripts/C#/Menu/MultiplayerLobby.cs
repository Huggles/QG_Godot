using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Multiplayer lobby: host/join + per-player faction selection.
/// Each player picks factions from one team (Axis or Allies).
/// Host can revoke any player's faction assignment.
/// All 6 factions must be assigned before the game can start.
/// </summary>
public partial class MultiplayerLobby : Control
{
    private const int DEFAULT_PORT = 7777;
    private const string DEFAULT_SERVER_IP = "127.0.0.1";

    // ── Faction metadata ───────────────────────────────────────────────────────
    private static readonly List<Faction> AllPlayableFactions = new()
    {
        Faction.GERMANY, Faction.JAPAN, Faction.ITALY,
        Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES
    };

    private static readonly HashSet<Faction> AxisSet = new()
        { Faction.GERMANY, Faction.JAPAN, Faction.ITALY };

    private static readonly Dictionary<Faction, string> FlagPaths = new()
    {
        { Faction.GERMANY,        "res://assets/factions/germany/Germany_Flag.png" },
        { Faction.JAPAN,          "res://assets/factions/japan/Japan_Flag.png" },
        { Faction.ITALY,          "res://assets/factions/italy/Italy_Flag.png" },
        { Faction.UNITED_KINGDOM, "res://assets/factions/united_kingdom/UK_Flag.png" },
        { Faction.SOVIET,         "res://assets/factions/soviet/Soviet_Flag.png" },
        { Faction.UNITED_STATES,  "res://assets/factions/united_states/US_Flag.png" },
    };

    private static readonly Dictionary<Faction, string> FactionNames = new()
    {
        { Faction.GERMANY,        "Germany" },
        { Faction.JAPAN,          "Japan" },
        { Faction.ITALY,          "Italy" },
        { Faction.UNITED_KINGDOM, "United Kingdom" },
        { Faction.SOVIET,         "Soviet Union" },
        { Faction.UNITED_STATES,  "United States" },
    };

    // ── Visual colours for button states ──────────────────────────────────────
    private static readonly Color ColClaimed     = new(1.0f, 0.85f, 0.2f, 1.0f);  // gold  – claimed by this row's player
    private static readonly Color ColAvailable   = Colors.White;                    // white – available & team-compatible
    private static readonly Color ColUnavailable = new(0.30f, 0.30f, 0.30f, 0.55f); // dark grey – wrong team
    private static readonly Color ColOtherOwned  = new(0.50f, 0.50f, 0.50f, 0.75f); // mid grey  – owned by another player

    // ── Scene node references ─────────────────────────────────────────────────
    private VBoxContainer _playerListContainer;
    private Button        _hostButton;
    private Button        _joinButton;
    private Button        _startGameButton;
    private Button        _debugSoloButton;
    private Label         _statusLabel;
    private LineEdit      _ipAddressInput;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private bool _isHost        = false;
    private bool _lobbyOnlyMode = false;

    /// <summary>peerId → name Label; text is also used as source for SyncPlayerList RPC.</summary>
    private readonly Dictionary<int, Label>          _playerLabels    = new();
    /// <summary>peerId → root PanelContainer of that player's row.</summary>
    private readonly Dictionary<int, PanelContainer> _playerRowPanels = new();
    /// <summary>peerId → { faction → TextureButton }.</summary>
    private readonly Dictionary<int, Dictionary<Faction, TextureButton>> _factionButtons = new();
    /// <summary>Authoritative faction assignments: faction → owning peerId.</summary>
    private readonly Dictionary<Faction, int> _assignments = new();

    // ══════════════════════════════════════════════════════════════════════════
    // Godot lifecycle
    // ══════════════════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        DebugUtilities.PrintPeerFinest("MultiplayerLobby: Ready");
        
        // Get UI references
        _playerListContainer = GetNode<VBoxContainer>("%PlayerListContainer");
        _hostButton          = GetNode<Button>("%HostButton");
        _joinButton          = GetNode<Button>("%JoinButton");
        _startGameButton     = GetNode<Button>("%StartGameButton");
        _debugSoloButton     = GetNode<Button>("%DebugSoloButton");
        _statusLabel         = GetNode<Label>("%StatusLabel");
        _ipAddressInput      = GetNode<LineEdit>("%IpAddressInput");
        
        _ipAddressInput.Text     = DEFAULT_SERVER_IP;
        _startGameButton.Visible = false;
        
        _hostButton.Pressed      += OnHostButtonPressed;
        _joinButton.Pressed      += OnJoinButtonPressed;
        _startGameButton.Pressed += OnStartGameButtonPressed;
        _debugSoloButton.Pressed += OnDebugSoloButtonPressed;
        
        Multiplayer.PeerConnected      += OnPeerConnected;
        Multiplayer.PeerDisconnected   += OnPeerDisconnected;
        Multiplayer.ConnectedToServer  += OnConnectedToServer;
        Multiplayer.ConnectionFailed   += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
        
        UpdateStatusLabel("Waiting to host or join...");

        _lobbyOnlyMode = OS.GetCmdlineUserArgs().Contains("lobby_only=true");
        int instance   = GetInstanceNumber();

        if (GameSettings.IsDebugMultiplayer && instance == 1)
        {
            DebugUtilities.PrintPeer(_lobbyOnlyMode
                ? "DebugMultiplayer Instance 1: auto-hosting (lobby only)..."
                : "DebugMultiplayer Instance 1: auto-hosting...");
            OnHostButtonPressed();
        }
        else if (GameSettings.IsDebugMultiplayer && instance == 2)
        {
            DebugUtilities.PrintPeer(_lobbyOnlyMode
                ? "DebugMultiplayer Instance 2: auto-joining (lobby only)..."
                : "DebugMultiplayer Instance 2: auto-joining...");
            _ipAddressInput.Text = DEFAULT_SERVER_IP;
            OnJoinButtonPressed();
        }
    }

    private static int GetInstanceNumber()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("instance=") && int.TryParse(arg.Substring("instance=".Length), out int n))
                return n;
        }
        return 0;
    }

    public override void _ExitTree()
    {
        Multiplayer.PeerConnected      -= OnPeerConnected;
        Multiplayer.PeerDisconnected   -= OnPeerDisconnected;
        Multiplayer.ConnectedToServer  -= OnConnectedToServer;
        Multiplayer.ConnectionFailed   -= OnConnectionFailed;
        Multiplayer.ServerDisconnected -= OnServerDisconnected;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Connection buttons
    // ══════════════════════════════════════════════════════════════════════════

    private void OnHostButtonPressed()
    {
        DebugUtilities.PrintPeer("Starting host...");
        
        var peer  = new ENetMultiplayerPeer();
        Error err = peer.CreateServer(DEFAULT_PORT, 6);
        if (err != Error.Ok)
        {
            DebugUtilities.PrintPeerError($"Failed to create server: {err}");
            UpdateStatusLabel($"Failed to host: {err}");
            return;
        }
        
        Multiplayer.MultiplayerPeer = peer;
        _isHost                     = true;
        DebugUtilities.PrintPeer($"Server started on port {DEFAULT_PORT}");
        UpdateStatusLabel($"Hosting on port {DEFAULT_PORT}");
        
        AddPlayerRow(1, "Player 1 (Host)");
        _startGameButton.Visible  = true;
        _startGameButton.Disabled = true; // unlocks once all 6 factions are assigned
        _hostButton.Disabled      = true;
        _joinButton.Disabled      = true;
    }

    private void OnJoinButtonPressed()
    {
        DebugUtilities.PrintPeer("Joining server...");
        string ip = _ipAddressInput.Text;
        
        var peer  = new ENetMultiplayerPeer();
        Error err = peer.CreateClient(ip, DEFAULT_PORT);
        if (err != Error.Ok)
        {
            DebugUtilities.PrintPeerError($"Failed to create client: {err}");
            UpdateStatusLabel($"Failed to join: {err}");
            return;
        }
        
        Multiplayer.MultiplayerPeer = peer;
        DebugUtilities.PrintPeer($"Connecting to {ip}:{DEFAULT_PORT}");
        UpdateStatusLabel($"Connecting to {ip}:{DEFAULT_PORT}...");
        _hostButton.Disabled = true;
        _joinButton.Disabled = true;
    }

    private void OnStartGameButtonPressed()
    {
        if (!_isHost)
        {
            DebugUtilities.PrintPeerError("Only the host can start the game");
            return;
        }
        if (!AllPlayableFactions.All(f => _assignments.ContainsKey(f)))
        {
            DebugUtilities.PrintPeerError("Cannot start: not all factions are assigned");
            return;
        }
        DebugUtilities.PrintPeer("Host starting game...");
        Rpc(nameof(StartGame));
    }

    private void OnDebugSoloButtonPressed()
    {
        DebugUtilities.PrintPeer("Starting debug solo game...");
        var list = new List<PlayerFactionAssignment>
        {
            new PlayerFactionAssignment(1, new List<Faction>(StaticGameData.PlayableFactions))
        };
        GetNode<GameManager>("/root/GameManager").SetPendingPlayerFactionAssignments(list);
        GetTree().ChangeSceneToFile("res://scenes/Game.tscn");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Faction selection — client side
    // ══════════════════════════════════════════════════════════════════════════

    private void OnFactionButtonPressed(int rowPeerId, Faction faction)
    {
        int me = Multiplayer.GetUniqueId();
        // Only the row's owner or the host may interact with a row.
        if (me != rowPeerId && me != 1) return;

        if (Multiplayer.IsServer())
            RequestFactionToggle(rowPeerId, (int)faction);
        else
            RpcId(1, nameof(RequestFactionToggle), rowPeerId, (int)faction);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Faction selection — server side (RPCs)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called on the host (directly or via RPC from a client).
    /// Validates and applies the requested faction toggle, then broadcasts the new state.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void RequestFactionToggle(int targetPeerId, int factionInt)
    {
        if (!Multiplayer.IsServer()) return;

        int requester = (int)Multiplayer.GetRemoteSenderId();
        if (requester == 0) requester = 1; // direct (local) call by host

        Faction faction       = (Faction)factionInt;
        bool    isClaimed     = _assignments.TryGetValue(faction, out int owner);
        bool    ownedByTarget = isClaimed && owner == targetPeerId;

        if (requester == 1 && targetPeerId != 1)
        {
            // Host acting on another player's row: revoke only.
            if (ownedByTarget)
                _assignments.Remove(faction);
        }
        else if (requester == targetPeerId)
        {
            if (ownedByTarget)
            {
                // Unclaim own faction.
                _assignments.Remove(faction);
            }
            else if (!isClaimed)
            {
                // Attempt to claim: enforce team constraint.
                FactionTeam playerTeam  = GetTeam(requester);
                FactionTeam factionTeam = AxisSet.Contains(faction) ? FactionTeam.AXIS : FactionTeam.ALLIES;
                if (playerTeam == FactionTeam.NONE || playerTeam == factionTeam)
                    _assignments[faction] = requester;
            }
        }

        Rpc(nameof(SyncFactionState), SerialiseAssignments());
    }

    /// <summary>
    /// Broadcast from the host to all peers (and locally): the authoritative assignment table.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
    private void SyncFactionState(Godot.Collections.Dictionary<int, int> data)
    {
        _assignments.Clear();
        foreach (var kv in data)
            _assignments[(Faction)kv.Key] = kv.Value;

        RefreshAllButtons();
        UpdateStartGameButton();
    }

    private void RefreshAllButtons()
    {
        foreach (var (peerId, buttons) in _factionButtons)
            foreach (var (faction, btn) in buttons)
                RefreshButton(peerId, faction, btn);
    }

    private void RefreshButton(int rowPeerId, Faction faction, TextureButton btn)
    {
        int  me      = Multiplayer.GetUniqueId();
        bool isMyRow = rowPeerId == me;
        bool iAmHost = me == 1;

        bool isClaimed    = _assignments.TryGetValue(faction, out int owner);
        bool ownedByRow   = isClaimed && owner == rowPeerId;
        bool ownedByOther = isClaimed && !ownedByRow;

        FactionTeam rowTeam     = GetTeam(rowPeerId);
        bool        isAxis      = AxisSet.Contains(faction);
        FactionTeam factionTeam = isAxis ? FactionTeam.AXIS : FactionTeam.ALLIES;
        bool        teamOk      = rowTeam == FactionTeam.NONE || rowTeam == factionTeam;

        if (ownedByRow)
        {
            // Gold: this row's player has claimed this faction.
            // Clickable by the owner (to unclaim) or the host (to revoke).
            btn.Modulate = ColClaimed;
            btn.Disabled = !(isMyRow || iAmHost);
        }
        else if (ownedByOther)
        {
            // Another player already owns this faction.
            btn.Modulate = ColOtherOwned;
            btn.Disabled = true;
        }
        else if (!isMyRow)
        {
            // Viewing another player's row; unclaimed factions are neutral/inactive.
            btn.Modulate = ColOtherOwned;
            btn.Disabled = true;
        }
        else if (!teamOk)
        {
            // My row, but this faction belongs to the opposing team.
            btn.Modulate = ColUnavailable;
            btn.Disabled = true;
        }
        else
        {
            // My row, faction available and team-compatible.
            btn.Modulate = ColAvailable;
            btn.Disabled = false;
        }
    }

    private FactionTeam GetTeam(int peerId)
    {
        if (_assignments.Any(kv => kv.Value == peerId && AxisSet.Contains(kv.Key)))
            return FactionTeam.AXIS;
        if (_assignments.Any(kv => kv.Value == peerId && !AxisSet.Contains(kv.Key)))
            return FactionTeam.ALLIES;
        return FactionTeam.NONE;
    }

    private Godot.Collections.Dictionary<int, int> SerialiseAssignments()
    {
        var d = new Godot.Collections.Dictionary<int, int>();
        foreach (var kv in _assignments) d[(int)kv.Key] = kv.Value;
        return d;
    }

    private void UpdateStartGameButton()
    {
        if (!_isHost) return;
        int missing = AllPlayableFactions.Count(f => !_assignments.ContainsKey(f));
        _startGameButton.Disabled    = missing > 0;
        _startGameButton.TooltipText = missing == 0
            ? "All factions assigned – ready to start!"
            : $"{missing} faction(s) still unassigned";
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Game start
    // ══════════════════════════════════════════════════════════════════════════

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void StartGame()
    {
        DebugUtilities.PrintPeer($"StartGame: peer {Multiplayer.GetUniqueId()}");

        var byPeer = new Dictionary<int, List<Faction>>();
        foreach (var (faction, peerId) in _assignments)
        {
            if (!byPeer.ContainsKey(peerId)) byPeer[peerId] = new List<Faction>();
            byPeer[peerId].Add(faction);
        }

        var playerFactionAssignments = byPeer
            .Select(kv => new PlayerFactionAssignment(kv.Key, kv.Value))
            .ToList();

        GetNode<GameManager>("/root/GameManager").SetPendingPlayerFactionAssignments(playerFactionAssignments);
        GetTree().ChangeSceneToFile("res://scenes/Game.tscn");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Multiplayer signal callbacks
    // ══════════════════════════════════════════════════════════════════════════

    private void OnPeerConnected(long peerId)
    {
        DebugUtilities.PrintPeer($"Peer connected: {peerId}");
        int    number     = _playerLabels.Count + 1;
        string playerName = $"Player {number}";
        AddPlayerRow((int)peerId, playerName);

        if (_isHost)
        {
            RpcId((int)peerId, nameof(SyncPlayerList),   GetPlayerListData());
            RpcId((int)peerId, nameof(SyncFactionState), SerialiseAssignments());

            if (GameSettings.IsDebugMultiplayer && !_lobbyOnlyMode)
            {
                // F6 debug auto-start: assign default factions, sync to all, then start.
                AutoAssignDebugFactions((int)peerId);
                Rpc(nameof(SyncFactionState), SerialiseAssignments());
                OnStartGameButtonPressed();
            }
        }
    }

    /// <summary>
    /// Default faction assignment for F6 debug auto-start:
    /// host (peer 1) → Axis, connecting client → Allies.
    /// </summary>
    private void AutoAssignDebugFactions(int clientPeerId)
    {
        _assignments.Clear();
        foreach (var f in AxisSet)                              _assignments[f] = 1;
        foreach (var f in AllPlayableFactions.Except(AxisSet)) _assignments[f] = clientPeerId;
    }

    private void OnPeerDisconnected(long peerId)
    {
        DebugUtilities.PrintPeer($"Peer disconnected: {peerId}");
        RemovePlayerRow((int)peerId);

        if (_isHost)
        {
            var released = _assignments
                .Where(kv => kv.Value == (int)peerId)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var f in released) _assignments.Remove(f);
            Rpc(nameof(SyncFactionState), SerialiseAssignments());
        }
    }

    private void OnConnectedToServer()
    {
        DebugUtilities.PrintPeer("Connected to server");
        int me = Multiplayer.GetUniqueId();
        UpdateStatusLabel("Connected – waiting for lobby data...");
        AddPlayerRow(me, $"Player {me} (You)");
    }

    private void OnConnectionFailed()
    {
        DebugUtilities.PrintPeerError("Connection failed");
        UpdateStatusLabel("Connection failed!");
        _hostButton.Disabled = false;
        _joinButton.Disabled = false;
    }

    private void OnServerDisconnected()
    {
        DebugUtilities.PrintPeerError("Server disconnected");
        UpdateStatusLabel("Disconnected from server");
        ClearAllRows();
        _assignments.Clear();
        _hostButton.Disabled     = false;
        _joinButton.Disabled     = false;
        _startGameButton.Visible = false;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Player row UI
    // ══════════════════════════════════════════════════════════════════════════

    private void AddPlayerRow(int peerId, string playerName)
    {
        if (_playerLabels.ContainsKey(peerId)) return;

        // ── Row background ────────────────────────────────────────────────
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor                 = new Color(0.15f, 0.15f, 0.15f, 0.55f),
            CornerRadiusTopLeft     = 4,
            CornerRadiusTopRight    = 4,
            CornerRadiusBottomLeft  = 4,
            CornerRadiusBottomRight = 4
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 10);
        panel.AddChild(hbox);

        // ── Player name ───────────────────────────────────────────────────
        var nameLabel = new Label
        {
            Text              = playerName,
            CustomMinimumSize = new Vector2(160, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        nameLabel.AddThemeColorOverride("font_color", Colors.White);
        hbox.AddChild(nameLabel);
        hbox.AddChild(new VSeparator());

        // ── Faction flags ─────────────────────────────────────────────────
        var flagsBox = new HBoxContainer();
        flagsBox.AddThemeConstantOverride("separation", 6);
        flagsBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(flagsBox);

        _factionButtons[peerId] = new Dictionary<Faction, TextureButton>();
        bool separatorAdded     = false;

        foreach (var faction in AllPlayableFactions)
        {
            // Visual separator between the Axis block (GER/JAP/ITA) and the Allies block.
            if (!separatorAdded && !AxisSet.Contains(faction))
            {
                flagsBox.AddChild(new VSeparator { CustomMinimumSize = new Vector2(2, 96) });
                separatorAdded = true;
            }

            var tex = GD.Load<Texture2D>(FlagPaths[faction]);
            var btn = new TextureButton
            {
                TextureNormal        = tex,
                TextureDisabled      = tex,    // colour state is driven entirely by Modulate
                StretchMode          = TextureButton.StretchModeEnum.KeepAspectCentered,
                IgnoreTextureSize    = true,
                CustomMinimumSize    = new Vector2(0, 96),
                SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill,
                TooltipText          = FactionNames[faction]
            };

            // Capture loop variables for the closure.
            int     capPeerId  = peerId;
            Faction capFaction = faction;
            btn.Pressed += () => OnFactionButtonPressed(capPeerId, capFaction);

            flagsBox.AddChild(btn);
            _factionButtons[peerId][faction] = btn;
        }

        _playerListContainer.AddChild(panel);
        _playerLabels[peerId]    = nameLabel;
        _playerRowPanels[peerId] = panel;

        // Initialise visual states for the new row based on current assignments.
        foreach (var (f, b) in _factionButtons[peerId])
            RefreshButton(peerId, f, b);

        DebugUtilities.PrintPeer($"Added player row: {playerName} (Peer {peerId})");
    }

    private void RemovePlayerRow(int peerId)
    {
        if (_playerRowPanels.TryGetValue(peerId, out var panel))
        {
            panel.QueueFree();
            _playerRowPanels.Remove(peerId);
        }
        _playerLabels.Remove(peerId);
        _factionButtons.Remove(peerId);
        DebugUtilities.PrintPeer($"Removed player row: Peer {peerId}");
    }

    private void ClearAllRows()
    {
        foreach (var panel in _playerRowPanels.Values) panel.QueueFree();
        _playerRowPanels.Clear();
        _playerLabels.Clear();
        _factionButtons.Clear();
    }

    private Godot.Collections.Dictionary<int, string> GetPlayerListData()
    {
        var d = new Godot.Collections.Dictionary<int, string>();
        foreach (var kv in _playerLabels) d[kv.Key] = kv.Value.Text;
        return d;
    }

    /// <summary>
    /// Rebuilds the entire player list from host-provided data.
    /// Called on newly connected clients to synchronise the current lobby state.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void SyncPlayerList(Godot.Collections.Dictionary<int, string> playerData)
    {
        ClearAllRows();
        foreach (var kv in playerData) AddPlayerRow(kv.Key, kv.Value);
        RefreshAllButtons();
    }

    private void UpdateStatusLabel(string msg)
    {
        _statusLabel.Text = msg;
        DebugUtilities.PrintPeer($"Lobby: {msg}");
    }
}
