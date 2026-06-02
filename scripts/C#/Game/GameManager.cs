using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

public partial class GameManager : Node
{
    private const string GameModeArg = "game_mode";
    private const string LocalMultiplayerGameArg = "local_multiplayer_game";

    private static readonly PackedScene GameLoadTransitionScene = GD.Load<PackedScene>("res://scenes/loading/game_loading_transition.tscn");
    private static readonly PackedScene UIScene = GD.Load<PackedScene>("res://scenes/userinterface/game_user_interface_base.tscn");
    

    private MultiplayerSpawner multiplayerSpawner = new MultiplayerSpawner();

    private Node3D _gameLoadTransitionScreenInstance;    

    public GameSession GameSession;

    private List<PlayerScene> playerStates = new List<PlayerScene>();
    private List<PlayerFactionAssignment> _pendingPlayerFactionAssignments;
    private bool _gameInitialized = false;

    public Camera2D MyCamera => GetViewport().GetCamera2D();
    public InputManager MyInputManager => playerStates.Count > 0 ? playerStates[0].InputManager : null;
    
    public List<string> UserInterfaceElementsLoaded = new List<string>();
    public List<string> UserInterfaceElementsToLoad = new List<string> {
        "PlayerActionLabel",
        "InputOptionsList",
        "FactionHandDisplay",
        "FactionsContainer",
        "PresentationModal",
        "PlayerInfoDisplay"
    };
    

    public override void _Ready()
    {
        DebugUtilities.PrintPeer("GameManager Ready");
        
        // Listen for scene changes
        GetTree().NodeAdded += OnNodeAdded;
    }

    public override void _ExitTree()
    {
        GetTree().NodeAdded -= OnNodeAdded;
    }

    private void OnNodeAdded(Node node)
    {
        // When the Game scene root is added, check for pending player-faction assignments
        if (node.Name == "Game" && node.SceneFilePath == "res://scenes/Game.tscn")
        {
            DebugUtilities.PrintPeer("Game scene detected");
            node.GetNode<PeerReadinessComponent>("PeerReadinessComponent").AllPeersReady += () =>
            {
                DebugUtilities.PrintPeer("All peers ready in Game scene - initializing game");
                InitializeGame(_pendingPlayerFactionAssignments);                                
            };
            node.Ready += () => node.GetNode<PeerReadinessComponent>("PeerReadinessComponent").RegisterReady();
        }
    }

    /// <summary>
    /// Sets the player-faction assignments that will be used when the game scene loads
    /// </summary>
    public void SetPendingPlayerFactionAssignments(List<PlayerFactionAssignment> assignments)
    {
        _pendingPlayerFactionAssignments = assignments;
        DebugUtilities.PrintPeer($"Stored pending faction assignments for {assignments.Count} player(s)");
    }

    /// <summary>
    /// Called when starting the actual game (after lobby)
    /// </summary>
    /// <param name="playerFactionAssignments">Dictionary mapping peer IDs to their assigned factions</param>
    public void InitializeGame(List<PlayerFactionAssignment> playerFactionAssignments)
    {
        // Clear pending assignments since we're using them now
        _pendingPlayerFactionAssignments = null;

        _ = LoadGame(playerFactionAssignments);
    }

    /// <summary>
    /// Loads the game with the specified player-to-faction assignments.
    /// </summary>
    /// <param name="playerFactionAssignments">Dictionary mapping peer IDs to their assigned factions</param>
    private async Task LoadGame(List<PlayerFactionAssignment> playerFactionAssignments)
    {
        DebugUtilities.PrintPeer("Multiplayer session started - waiting for clients to initialize");
        await NetworkApi.Instance.StartMultiplayerSession(JsonSerializer.Serialize(playerFactionAssignments));
        DebugUtilities.PrintPeer("Multiplayer session initialization complete - waiting for UI elements to load");

        MultiplayerSession.Instance.StartNew(JsonSerializer.Serialize(playerFactionAssignments));
    }
}
