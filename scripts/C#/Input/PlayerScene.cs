using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PlayerScene : CharacterBody2D
{
    [Export] private string _playerName = "Player_";

    public static PlayerScene Current { get; private set; }

    private Camera2D _camera => GetNode<Camera2D>("%MainGameCamera");
    private Node _rootNode => GetNode(".");

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
        }
        DebugUtilities.PrintPeerFinest($"PlayerScene ready: {PlayerName}");
        GetNode<PeerReadinessComponent>("PeerReadinessComponent").RegisterReady();

        AimFocusCameraAtMainCamera();
    }

    /// <summary>
    /// Parks the picture-in-picture camera on whatever the main camera is looking at, so the PiP
    /// opens on the board instead of on empty space: FocusCamera sits in the shared World2D at its
    /// own coordinates, and (0, 0) is nowhere near the board.
    /// </summary>
    private void AimFocusCameraAtMainCamera()
    {
        // No render target headless, so the whole FocusLayer is dead weight there.
        if (GameContext.IsHeadless) return;

        Camera2D focusCamera = GetNodeOrNull<Camera2D>("%FocusCamera");
        if (focusCamera == null) return;

        focusCamera.GlobalPosition = _camera.GlobalPosition;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            DebugUtilities.PrintPeerFinest($"Player deleted. Name: {_playerName}");
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

    /// <summary>
    /// The human name to show beside this player's factions ("Bob"), or null when there is none.
    ///
    /// Deliberately NOT folded into <see cref="PlayerName"/>: that setter also writes the Godot node
    /// Name, which is part of the NodePath Godot resolves RPCs against (PeerReadinessComponent is
    /// fetched as a child of this node), so it has to stay a deterministic, collision-free identifier.
    /// A Steam persona is neither — it can hold spaces, unicode, and duplicates.
    ///
    /// Set identically on every peer: NetworkApi.LoadPlayers deserialises the host's assignment list
    /// everywhere, so a name resolved on the host matches what a client would resolve locally.
    /// </summary>
    public string DisplayName { get; set; }

    public override void _EnterTree()
    {
        base._EnterTree();
        DebugUtilities.PrintPeerFinest("Entered tree player");
    }

    /// <summary>Instantiates the in-game HUD once (idempotent). The full-screen cover is owned by
    /// <see cref="GameManager"/> and dropped separately, once every setup ChangeEvent has applied.</summary>
    public void EnsureUiLoaded()
    {
        // No UI to build headless. Without this a CLI run — where PlayerScene.Current is non-null,
        // unlike on a dedicated server — would instantiate the whole user_interface.tscn.
        if (GameContext.IsHeadless) return;

        if (!_uiLoaded)
            LoadUI();
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
