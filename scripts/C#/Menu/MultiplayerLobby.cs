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
        _statusLabel = GetNode<Label>("%StatusLabel");
        _ipAddressInput = GetNode<LineEdit>("%IpAddressInput");
        
        // Set default values
        _ipAddressInput.Text = DEFAULT_SERVER_IP;
        _startGameButton.Visible = false;
        
        // Connect button signals
        _hostButton.Pressed += OnHostButtonPressed;
        _joinButton.Pressed += OnJoinButtonPressed;
        _startGameButton.Pressed += OnStartGameButtonPressed;
        
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
        DebugUtilities.PrintPeer("Starting host...");
        
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
        
        DebugUtilities.PrintPeer($"Server started on port {DEFAULT_PORT}");
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
        DebugUtilities.PrintPeer("Joining server...");
        
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
        
        DebugUtilities.PrintPeer($"Connecting to {ip}:{DEFAULT_PORT}");
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
        
        DebugUtilities.PrintPeer("Host starting game...");
        
        // Tell all clients to start the game
        Rpc(nameof(StartGame));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void StartGame()
    {
        DebugUtilities.PrintPeer($"Starting game for peer {Multiplayer.GetUniqueId()}");
        
        // Load the main game scene
        GetTree().ChangeSceneToFile("res://scenes/Game.tscn");
    }

    // ==================== MULTIPLAYER CALLBACKS ====================

    private void OnPeerConnected(long peerId)
    {
        DebugUtilities.PrintPeer($"Peer connected: {peerId}");
        
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
        DebugUtilities.PrintPeer($"Peer disconnected: {peerId}");
        RemovePlayerFromList((int)peerId);
    }

    private void OnConnectedToServer()
    {
        DebugUtilities.PrintPeer("Successfully connected to server");
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
