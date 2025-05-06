using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerScene : CharacterBody3D
{
    [Export]
    private int _peerId;
    [Export]
    private string _playerName;
    [Export]
    public Godot.Collections.Array FactionStrings;

    public InputManager InputManager => GetNode("%InputManager") as InputManager; 

    private List<FactionData> factions;
    public List<FactionData> Factions
    {
        get
        {
            if (factions == null || factions.Count == 0 || factions.Count != FactionStrings.Count)
            {
                factions = new List<FactionData>(); 
            }
            return factions;
        }
    }
    
    private Camera3D _camera;
    private Node _rootNode;
    

    public override void _Ready()
    {
        _rootNode = GetNode(".");  // Same as $"." in GDScript
        _camera = GetNode<Camera3D>("Camera3D");

        if (_peerId == Multiplayer.GetUniqueId())
        {
            DebugUtilities.PrintPeer($"Setting camera for player: {Name}");
            _camera.Current = true;
        }

        if (FactionStrings == null || FactionStrings.Count == 0)
        {
            FactionStrings = new Godot.Collections.Array {
                "GERMANY", "UNITED_KINGDOM", "JAPAN", "SOVIET", "ITALY", "UNITED_STATES"
            };
        }

        DebugUtilities.PrintPeer("Adding player");        
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
            SetMultiplayerAuthority(1);
            GetNode<MultiplayerSynchronizer>("ServerSynchronizer").SetMultiplayerAuthority(1);
            GetNode<MultiplayerSynchronizer>("PlayerSynchronizer").SetMultiplayerAuthority(value);
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
}
