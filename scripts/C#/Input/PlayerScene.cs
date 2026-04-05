using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PlayerScene : CharacterBody2D
{
    [Export]
    private int _peerId = 1;
    [Export]
    private string _playerName = "Player";
    
    /// <summary>
    /// The factions this player controls in the game
    /// Set by PlayerFactionRegistry during game setup
    /// </summary>
    private List<Faction> _controlledFactions = new List<Faction>();
    
    public InputManager InputManager => GetNode("%InputManager") as InputManager; 

    /// <summary>
    /// Get the factions controlled by this player
    /// </summary>
    public List<Faction> ControlledFactions => new List<Faction>(_controlledFactions);
    
    /// <summary>
    /// Set the factions controlled by this player
    /// </summary>
    public void SetControlledFactions(List<Faction> factions)
    {
        _controlledFactions = new List<Faction>(factions);
        DebugUtilities.PrintPeer($"PlayerScene ({PlayerName}, Peer {PeerId}): Controls {string.Join(", ", factions)}");
    }
    
    /// <summary>
    /// Check if this player controls a specific faction
    /// </summary>
    public bool ControlsFaction(Faction faction)
    {
        return _controlledFactions.Contains(faction);
    }
    
    /// <summary>
    /// Check if this player controls any faction in a team
    /// </summary>
    public bool ControlsTeam(FactionTeam team)
    {
        return _controlledFactions.Any(f => StaticGameData.FactionTeamForFaction(f) == team);
    }
    
    private Camera2D _camera;
    private Node _rootNode;
    
    private static Godot.Vector2 DEFAULT_POSITION = new Godot.Vector2(6321,1584);
    private static Godot.Vector2 DEFAULT_ZOOM = new Godot.Vector2(6321,1584);
    

    public override void _Ready()
    {
        _rootNode = GetNode(".");  // Same as $"." in GDScript
        _camera = GetNode<Camera2D>("Camera2D");

        // Set multiplayer authorities now that we're in the tree
        SetMultiplayerAuthority(1);
        
        var serverSync = GetNodeOrNull<MultiplayerSynchronizer>("ServerSynchronizer");
        if (serverSync != null)
        {
            serverSync.SetMultiplayerAuthority(1);
        }
        
        var playerSync = GetNodeOrNull<MultiplayerSynchronizer>("PlayerSynchronizer");
        if (playerSync != null)
        {
            playerSync.SetMultiplayerAuthority(_peerId);
        }

        // Set camera active for local player
        if (_peerId == Multiplayer.GetUniqueId())
        {
            DebugUtilities.PrintPeer($"Setting camera for player: {PlayerName} (Peer {_peerId})");
            _camera.MakeCurrent();
        }

        DebugUtilities.PrintPeer($"PlayerScene ready: {PlayerName} (Peer {_peerId})");
    }

    public override void _PhysicsProcess(double delta)
    {
        HandleInput();
    }

    private void HandleInput()
    {
        // Placeholder for player input logic
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            GD.Print($"Player deleted. Name: {_playerName}");
        }
    }

    public int PeerId
    {
        get => _peerId;
        set
        {
            _peerId = value;
            
            // Only set authorities if the node is in the scene tree
            if (IsInsideTree())
            {
                SetMultiplayerAuthority(1);
                
                var serverSync = GetNodeOrNull<MultiplayerSynchronizer>("ServerSynchronizer");
                if (serverSync != null)
                {
                    serverSync.SetMultiplayerAuthority(1);
                }
                
                var playerSync = GetNodeOrNull<MultiplayerSynchronizer>("PlayerSynchronizer");
                if (playerSync != null)
                {
                    playerSync.SetMultiplayerAuthority(value);
                }
            }
        }
    }

    public string PlayerName
    {
        get => _playerName;
        set
        {
            _playerName = value;
            Name = _playerName;
        }
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        GD.Print("Entered tree player");
    }

}
