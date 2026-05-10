using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Multiplayer lobby scene - handles host/join and player list
/// Loads on game start before the actual game begins
/// </summary>
public partial class MultiplayerLobby : Control
{
    private const int DEFAULT_PORT = 7777;
    private const string DEFAULT_SERVER_IP = "127.0.0.1"; // localhost for testing on same machine
    
    // UI Elements
    private VBoxContainer _playerListContainer;
    private Button _hostButton;
    private Button _joinButton;
    private Button _startGameButton;
    private Button _debugSoloButton;
    private Label _statusLabel;
    private LineEdit _ipAddressInput;
    
    // State
    private Dictionary<int, Label> _playerLabels = new Dictionary<int, Label>();
    private bool _isHost = false;

    public override void _Ready()
    {
        DebugUtilities.PrintPeer("MultiplayerLobby: Ready");
        
        // Get UI references
        _playerListContainer = GetNode<VBoxContainer>("%PlayerListContainer");
        _hostButton = GetNode<Button>("%HostButton");
        _joinButton = GetNode<Button>("%JoinButton");
        _startGameButton = GetNode<Button>("%StartGameButton");
        _debugSoloButton = GetNode<Button>("%DebugSoloButton");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _ipAddressInput = GetNode<LineEdit>("%IpAddressInput");
        
        // Set default values
        _ipAddressInput.Text = DEFAULT_SERVER_IP;
        _startGameButton.Visible = false;
        
        // Connect button signals
        _hostButton.Pressed += OnHostButtonPressed;
        _joinButton.Pressed += OnJoinButtonPressed;
        _startGameButton.Pressed += OnStartGameButtonPressed;
        _debugSoloButton.Pressed += OnDebugSoloButtonPressed;
        
        // Connect multiplayer signals
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
        
        UpdateStatusLabel("Waiting to host or join...");
    }

    public override void _ExitTree()
    {
        // Disconnect signals
        Multiplayer.PeerConnected -= OnPeerConnected;
        Multiplayer.PeerDisconnected -= OnPeerDisconnected;
        Multiplayer.ConnectedToServer -= OnConnectedToServer;
        Multiplayer.ConnectionFailed -= OnConnectionFailed;
        Multiplayer.ServerDisconnected -= OnServerDisconnected;
    }

    private void OnHostButtonPressed()
    {
        DebugUtilities.PrintPeer("Starting host...", DebugVerbosity.INFO);
        
        var peer = new ENetMultiplayerPeer();
        Error error = peer.CreateServer(DEFAULT_PORT, 6); // Max 6 players (one per faction)
        
        if (error != Error.Ok)
        {
            DebugUtilities.PrintPeerError($"Failed to create server: {error}");
            UpdateStatusLabel($"Failed to host: {error}");
            return;
        }
        
        Multiplayer.MultiplayerPeer = peer;
        _isHost = true;
        
        DebugUtilities.PrintPeer($"Server started on port {DEFAULT_PORT}", DebugVerbosity.INFO);
        UpdateStatusLabel($"Hosting on port {DEFAULT_PORT}");
        
        // Add host to player list
        AddPlayerToList(1, "Player 1 (Host)");
        
        // Show start game button for host
        _startGameButton.Visible = true;
        
        // Disable host/join buttons
        _hostButton.Disabled = true;
        _joinButton.Disabled = true;
    }

    private void OnJoinButtonPressed()
    {
        DebugUtilities.PrintPeer("Joining server...", DebugVerbosity.INFO);
        
        string ip = _ipAddressInput.Text;
        
        var peer = new ENetMultiplayerPeer();
        Error error = peer.CreateClient(ip, DEFAULT_PORT);
        
        if (error != Error.Ok)
        {
            DebugUtilities.PrintPeerError($"Failed to create client: {error}");
            UpdateStatusLabel($"Failed to join: {error}");
            return;
        }
        
        Multiplayer.MultiplayerPeer = peer;
        _isHost = false;
        
        DebugUtilities.PrintPeer($"Connecting to {ip}:{DEFAULT_PORT}", DebugVerbosity.INFO);
        UpdateStatusLabel($"Connecting to {ip}:{DEFAULT_PORT}...");
        
        // Disable host/join buttons
        _hostButton.Disabled = true;
        _joinButton.Disabled = true;
    }

    private void OnStartGameButtonPressed()
    {
        if (!_isHost)
        {
            DebugUtilities.PrintPeerError("Only host can start the game");
            return;
        }
        
        DebugUtilities.PrintPeer("Host starting game...", DebugVerbosity.INFO);
        
        // Tell all clients to start the game
        Rpc(nameof(StartGame));
    }

    private void OnDebugSoloButtonPressed()
    {
        DebugUtilities.PrintPeer("Starting debug solo game...", DebugVerbosity.INFO);
        
        // Create single player assignment (all factions to player 1)
        var playerFactionAssignments = new Dictionary<int, List<Faction>>
        {
            { 1, new List<Faction>(StaticGameData.PlayableFactions) }
        };
        
        // Get GameManager and set pending assignments
        var gameManager = GetNode<GameManager>("/root/GameManager");
        gameManager.SetPendingPlayerFactionAssignments(playerFactionAssignments);
        
        // Load the main game scene
        GetTree().ChangeSceneToFile("res://scenes/Game.tscn");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void StartGame()
    {
        DebugUtilities.PrintPeer($"Starting game for peer {Multiplayer.GetUniqueId()}", DebugVerbosity.INFO);
        
        // Determine player-faction assignments based on player count
        int totalPlayers = _playerLabels.Count;
        DebugUtilities.PrintPeer($"Total players in lobby: {totalPlayers}");
        
        Dictionary<int, List<Faction>> playerFactionAssignments = CreatePlayerFactionAssignments(totalPlayers);
        
        // Get GameManager and set pending assignments
        var gameManager = GetNode<GameManager>("/root/GameManager");
        gameManager.SetPendingPlayerFactionAssignments(playerFactionAssignments);
        
        // Load the main game scene
        GetTree().ChangeSceneToFile("res://scenes/Game.tscn");
    }
    
    /// <summary>
    /// Creates player-faction assignments based on player count
    /// </summary>
    private Dictionary<int, List<Faction>> CreatePlayerFactionAssignments(int playerCount)
    {
        var assignments = new Dictionary<int, List<Faction>>();
        
        if (playerCount == 2)
        {
            // 2-player: Player 1 (host) gets AXIS, Player 2 gets ALLIES
            var allPeerIds = new List<int>(_playerLabels.Keys);
            allPeerIds.Sort();
            
            int player1Id = allPeerIds[0]; // Host
            int player2Id = allPeerIds[1]; // Other player
            
            List<Faction> axisFactions = new List<Faction> { Faction.GERMANY, Faction.JAPAN, Faction.ITALY };
            List<Faction> alliesFactions = new List<Faction> { Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES };
            
            assignments[player1Id] = axisFactions;
            assignments[player2Id] = alliesFactions;
            
            DebugUtilities.PrintPeer($"Player {player1Id} (Host) assigned AXIS", DebugVerbosity.INFO);
            DebugUtilities.PrintPeer($"Player {player2Id} assigned ALLIES", DebugVerbosity.INFO);
        }
        else if (playerCount >= 6)
        {
            // 6-player: Each player gets one faction
            var allPeerIds = new List<int>(_playerLabels.Keys);
            allPeerIds.Sort();
            
            int factionIndex = 0;
            foreach (var peerId in allPeerIds)
            {
                if (factionIndex < StaticGameData.PlayableFactions.Count)
                {
                    assignments[peerId] = new List<Faction> { StaticGameData.PlayableFactions[factionIndex] };
                    DebugUtilities.PrintPeer($"Player {peerId} assigned {StaticGameData.PlayableFactions[factionIndex]}", DebugVerbosity.INFO);
                    factionIndex++;
                }
            }
        }
        else
        {
            // Unsupported player count
            DebugUtilities.PrintPeerError($"Unsupported player count: {playerCount}. Multiplayer requires 2 or 6 players.");
            // Return empty assignments - this will cause an error, which is appropriate for unsupported configs
        }
        
        return assignments;
    }

    // ==================== MULTIPLAYER CALLBACKS ====================

    private void OnPeerConnected(long peerId)
    {
        DebugUtilities.PrintPeer($"Peer connected: {peerId}", DebugVerbosity.INFO);
        
        // Add player to list
        int playerNumber = _playerLabels.Count + 1;
        string playerName = peerId == 1 ? "Player 1 (Host)" : $"Player {playerNumber}";
        AddPlayerToList((int)peerId, playerName);
        
        // If we're the host, sync the current player list to the new peer
        if (_isHost)
        {
            RpcId((int)peerId, nameof(SyncPlayerList), GetPlayerListData());
        }
    }

    private void OnPeerDisconnected(long peerId)
    {
        DebugUtilities.PrintPeer($"Peer disconnected: {peerId}", DebugVerbosity.INFO);
        RemovePlayerFromList((int)peerId);
    }

    private void OnConnectedToServer()
    {
        DebugUtilities.PrintPeer("Successfully connected to server", DebugVerbosity.INFO);
        int myPeerId = Multiplayer.GetUniqueId();
        UpdateStatusLabel($"Connected as Player {myPeerId}");
        
        // Add ourselves to the list
        AddPlayerToList(myPeerId, $"Player {myPeerId} (You)");
    }

    private void OnConnectionFailed()
    {
        DebugUtilities.PrintPeerError("Connection to server failed");
        UpdateStatusLabel("Connection failed!");
        
        // Re-enable buttons
        _hostButton.Disabled = false;
        _joinButton.Disabled = false;
    }

    private void OnServerDisconnected()
    {
        DebugUtilities.PrintPeerError("Server disconnected");
        UpdateStatusLabel("Disconnected from server");
        
        // Clear player list
        foreach (var label in _playerLabels.Values)
        {
            label.QueueFree();
        }
        _playerLabels.Clear();
        
        // Re-enable buttons
        _hostButton.Disabled = false;
        _joinButton.Disabled = false;
        _startGameButton.Visible = false;
    }

    // ==================== PLAYER LIST MANAGEMENT ====================

    private void AddPlayerToList(int peerId, string playerName)
    {
        if (_playerLabels.ContainsKey(peerId))
        {
            return; // Already in list
        }
        
        var label = new Label();
        label.Text = playerName;
        label.AddThemeColorOverride("font_color", Colors.White);
        _playerListContainer.AddChild(label);
        _playerLabels[peerId] = label;
        
        DebugUtilities.PrintPeer($"Added to player list: {playerName} (Peer {peerId})");
    }

    private void RemovePlayerFromList(int peerId)
    {
        if (_playerLabels.TryGetValue(peerId, out Label label))
        {
            label.QueueFree();
            _playerLabels.Remove(peerId);
            DebugUtilities.PrintPeer($"Removed from player list: Peer {peerId}");
        }
    }

    private Godot.Collections.Dictionary<int, string> GetPlayerListData()
    {
        var playerData = new Godot.Collections.Dictionary<int, string>();
        foreach (var kvp in _playerLabels)
        {
            playerData[kvp.Key] = kvp.Value.Text;
        }
        return playerData;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void SyncPlayerList(Godot.Collections.Dictionary<int, string> playerData)
    {
        // Clear existing list
        foreach (var label in _playerLabels.Values)
        {
            label.QueueFree();
        }
        _playerLabels.Clear();
        
        // Add all players from sync data
        foreach (var kvp in playerData)
        {
            AddPlayerToList(kvp.Key, kvp.Value);
        }
    }

    private void UpdateStatusLabel(string status)
    {
        _statusLabel.Text = status;
        DebugUtilities.PrintPeer($"Lobby status: {status}");
    }
}
