using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PlayerScene : CharacterBody2D
{
    [Export] private string _playerName = "Player_";

    public static PlayerScene Current { get; private set; }

    private Camera2D _camera => GetNode<Camera2D>("Camera2D");
    private Node _rootNode => GetNode("."); 

    private CanvasLayer _loadingCoverInterface;

    private CanvasLayer _interfaceLayer => GetNodeOrNull<CanvasLayer>("Interface");     
    private bool _uiLoaded => GetChildren().ToList().Find(c => c.Name == "Interface") != null;

    private static Godot.Vector2 DEFAULT_POSITION = new Godot.Vector2(6321,1584);
    private static Godot.Vector2 DEFAULT_ZOOM = new Godot.Vector2(6321,1584);
    
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
        DebugUtilities.PrintPeer($"PlayerScene ({PlayerName}: Controls {string.Join(", ", factions)}");
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

    public override void _Ready()
    {   
        if(Multiplayer.GetUniqueId() == GetMultiplayerAuthority())
        {
            Current = this;
            _loadingCoverInterface = AssetRepository.LoadingCoverInterfaceScenePacked.Instantiate<CanvasLayer>();
            _loadingCoverInterface.Name = "LoadingCoverInterface";
            AddChild(_loadingCoverInterface);
        } 
        DebugUtilities.PrintPeer($"PlayerScene ready: {PlayerName}");
        GetNode<PeerReadinessComponent>("PeerReadinessComponent").RegisterReady();
        
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
        if (!_uiLoaded)
        {
            LoadUI();     
            if (_loadingCoverInterface != null)
            {
                var loadingCover = _loadingCoverInterface.GetNodeOrNull<Control>("LoadingCover");

                var tween = GetTree().CreateTween();   
                PropertyTweener propertyTweener1 = tween.TweenProperty(loadingCover, "modulate:a", 0.0, GameSettings.AnimationDurationSeconds)
                    .SetTrans(Tween.TransitionType.Sine)
                    .SetEase(Tween.EaseType.InOut);
                propertyTweener1.Finished += () => {
                    DebugUtilities.PrintPeer("Removing loading cover");
                    RemoveChild(_loadingCoverInterface);
                };
            } else {
                DebugUtilities.PrintPeerError("LoadingCover not found on PlayerScene");
            }    
        }   
    }

    public void LoadUI()
    {        
        CanvasLayer uiLayer = AssetRepository.UserInterfaceScenePacked.Instantiate() as CanvasLayer;
        uiLayer.Name = "Interface";
        uiLayer.SetMultiplayerAuthority(GetMultiplayerAuthority());
        uiLayer.Ready += () => {
            EventBus.Emit(EventBus.SignalName.UserInterfaceReady);
        };
        AddChild(uiLayer);
    }
}
