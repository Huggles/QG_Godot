using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PlayerScene : CharacterBody2D
{
    [Export]
    private string _playerName = "Player";

    public static PlayerScene Current { get; private set; }
    
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
        DebugUtilities.PrintPeer($"PlayerScene ({PlayerName}: Controls {string.Join(", ", factions)}", DebugVerbosity.INFO);
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
        GetNode<PeerReadinessComponent>("PeerReadinessComponent").RegisterReady();

        _rootNode = GetNode(".");  // Same as $"." in GDScript
        _camera = GetNode<Camera2D>("Camera2D");

        if(Multiplayer.GetUniqueId() == GetMultiplayerAuthority())
        {
            Current = this;
        }
        DebugUtilities.PrintPeer($"PlayerScene ready: {PlayerName}", DebugVerbosity.INFO);
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
            DebugUtilities.PrintPeer($"Player deleted. Name: {_playerName}");
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
        DebugUtilities.PrintPeer("Entered tree player");
    }

    public void FadeLoadingScreen()
    {
        DebugUtilities.PrintPeer("FadeLoadingScreen", DebugVerbosity.INFO);
        var loadingCover = GetNodeOrNull("%LoadingCover");        
        if (loadingCover != null)
        {
            var tween = GetTree().CreateTween();   
            PropertyTweener propertyTweener1 = tween.TweenProperty(loadingCover, "modulate:a", 0.0, GameSettings.AnimationDurationSeconds)
                 .SetTrans(Tween.TransitionType.Sine)
                 .SetEase(Tween.EaseType.InOut);
            propertyTweener1.Finished += () => {
                DebugUtilities.PrintPeer("Removing loading cover", DebugVerbosity.INFO);
                loadingCover?.GetParent()?.RemoveChild(loadingCover);
            };
        } else {
            DebugUtilities.PrintPeerError("LoadingCover not found on PlayerScene");
        }        
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public async void DoDebugCall()
    {
        DebugUtilities.PrintPeer($"DoDebugCall RPC received on PlayerScene for {PlayerName}", DebugVerbosity.INFO);
    }

}
