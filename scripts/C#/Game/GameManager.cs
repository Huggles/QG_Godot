using Godot;
using System;
using System.Collections.Generic;
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
    private static readonly PackedScene PlayerScene = GD.Load<PackedScene>("res://scenes/Player/Player.tscn");

    private Node3D _gameLoadTransitionScreenInstance;    

    [Export]
    public GameSession GameSession;

    private List<PlayerScene> playerStates = new List<PlayerScene>();
    private Dictionary<int, List<Faction>> _pendingPlayerFactionAssignments;
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
        
        // Check if we're already in the Game scene (e.g., if started directly)
        CheckAndInitializeGame();
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
            
            if (_pendingPlayerFactionAssignments != null)
            {
                DebugUtilities.PrintPeer("Found pending faction assignments - initializing game");
                // Use CallDeferred to ensure all nodes are ready
                CallDeferred(nameof(InitializeGameWithPending));
            }
            else
            {
                DebugUtilities.PrintPeer("No pending assignments - waiting for manual InitializeGame call");
            }
        }
    }
    
    private void InitializeGameWithPending()
    {
        if (_pendingPlayerFactionAssignments != null)
        {
            InitializeGame(_pendingPlayerFactionAssignments);
        }
    }

    private void CheckAndInitializeGame()
    {
        string currentScene = GetTree().CurrentScene?.SceneFilePath ?? "";
        DebugUtilities.PrintPeer($"Current scene: {currentScene}");
        
        if (currentScene.Contains("Game.tscn"))
        {
            DebugUtilities.PrintPeer("In game scene - checking for pending initialization");
            if (_pendingPlayerFactionAssignments != null)
            {
                InitializeGame(_pendingPlayerFactionAssignments);
            }
        }
        else if (currentScene.Contains("MultiplayerLobby"))
        {
            DebugUtilities.PrintPeer("In lobby scene - waiting for game start");
        }
    }

    /// <summary>
    /// Sets the player-faction assignments that will be used when the game scene loads
    /// </summary>
    public void SetPendingPlayerFactionAssignments(Dictionary<int, List<Faction>> assignments)
    {
        _pendingPlayerFactionAssignments = assignments;
        DebugUtilities.PrintPeer($"Stored pending faction assignments for {assignments.Count} player(s)");
    }

    /// <summary>
    /// Called when starting the actual game (after lobby)
    /// </summary>
    /// <param name="playerFactionAssignments">Dictionary mapping peer IDs to their assigned factions</param>
    public void InitializeGame(Dictionary<int, List<Faction>> playerFactionAssignments)
    {
        DebugUtilities.PrintPeer("Initializing game");
        
        // Clear pending assignments since we're using them now
        _pendingPlayerFactionAssignments = null;
        
        // Initialize NodeUtilities to find Game scene nodes
        NodeUtilities.Instance.InitializeGameNodes();
        
        LoadGame(playerFactionAssignments);
    }

    /// <summary>
    /// Loads the game with the specified player-to-faction assignments.
    /// </summary>
    /// <param name="playerFactionAssignments">Dictionary mapping peer IDs to their assigned factions</param>
    private void LoadGame(Dictionary<int, List<Faction>> playerFactionAssignments)
    {
        DebugUtilities.PrintPeer($"Loading game with {playerFactionAssignments.Count} player(s)");
        
        if (NodeUtilities.Instance.PlayersNode == null)
        {
            DebugUtilities.PrintPeerError("PlayersNode is null! Cannot add players.");
            return;
        }
        
        // Clear any stale UI loaded state from previous runs
        UserInterfaceElementsLoaded.Clear();
        _gameInitialized = false;
        
        // Step 1: Set up UI loading event handler BEFORE creating players
        // (UI elements emit events when Player scene is added to tree)
        DebugUtilities.PrintPeer("Setting up UserInterfaceLoaded event handler");
        EventBus.Instance.UserInterfaceLoaded += OnUserInterfaceElementLoaded;
        DebugUtilities.PrintPeer("Event handler registered");
        
        // Step 2: Create and register all players
        foreach (var entry in playerFactionAssignments)
        {
            int peerId = entry.Key;
            List<Faction> factions = entry.Value;
            
            string factionNames = string.Join(", ", factions);
            DebugUtilities.PrintPeer($"Creating player for peer {peerId} with factions: {factionNames}");
            
            PlayerScene player = PlayerScene.Instantiate<PlayerScene>();
            player.PeerId = peerId;
            player.PlayerName = factions.Count == 1 
                ? $"{factions[0]} Player" 
                : $"Player {peerId}";
            
            NodeUtilities.Instance.PlayersNode.AddChild(player);
            playerStates.Add(player);
            PlayerFactionRegistry.RegisterPlayer(player);
            
            DebugUtilities.PrintPeer($"Player {peerId} added to scene tree");
        }
        
        // Step 3: Assign factions to players
        foreach (var entry in playerFactionAssignments)
        {
            int peerId = entry.Key;
            List<Faction> factions = entry.Value;
            
            DebugUtilities.PrintPeer($"Assigning {factions.Count} faction(s) to peer {peerId}");
            PlayerFactionRegistry.AssignFactionsToPlayer(peerId, factions);
        }
        
        PlayerFactionRegistry.PrintStatus();
        
        // Notify UI that factions have been assigned
        EventBus.Emit(EventBus.SignalName.FactionsAssigned);
        
        // Step 4: Check if all UI elements have loaded
        // (Some may have already emitted during player creation)
        CallDeferred(nameof(CheckIfAllUIElementsLoaded));
    }
    
    private void OnUserInterfaceElementLoaded(string elementName)
    {
        DebugUtilities.PrintPeer($"UserInterfaceLoaded: {elementName}");
        
        if (!UserInterfaceElementsLoaded.Contains(elementName))
        {
            UserInterfaceElementsLoaded.Add(elementName);
        }
        
        CheckIfAllUIElementsLoaded();
    }
    
    private void CheckIfAllUIElementsLoaded()
    {
        // Prevent multiple initializations
        if (_gameInitialized)
        {
            return;
        }

        bool areEqual = new HashSet<string>(UserInterfaceElementsLoaded).SetEquals(UserInterfaceElementsToLoad);
        
        if (areEqual)
        {
            _gameInitialized = true;
            
            DebugUtilities.PrintPeer("All UI elements loaded - starting game");
            
            // Disconnect the event to prevent duplicate calls
            EventBus.Instance.UserInterfaceLoaded -= OnUserInterfaceElementLoaded;
            
            DebugUtilities.PrintPeer("LoadUI");
            LoadUI();

            DebugUtilities.PrintPeer("SetupGameSession");
            SetupGameSession();
        }
        else
        {
            var missing = UserInterfaceElementsToLoad.Except(UserInterfaceElementsLoaded).ToList();
            DebugUtilities.PrintPeer($"Waiting for {missing.Count} UI elements: {string.Join(", ", missing)}");
        }
    }

    private void LoadUI()
    {
        foreach (string loadedUiElement in UserInterfaceElementsLoaded)
        {
            Type type = Type.GetType(loadedUiElement);
            FieldInfo field = type.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
            LoadableUI loadableUI = field.GetValue(null) as LoadableUI;
            loadableUI.LoadUI();
        }
        
    }

    private void SetupGameSession()
    {
        DebugUtilities.PrintPeer("_setup_game_mode");
        GameSession = new GameSession();
        _ = GameSession.StartSession(playerStates);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void _SwitchLevel()
    {
        DebugUtilities.PrintPeer("Switch level");

        if (_gameLoadTransitionScreenInstance != null)
        {
            _gameLoadTransitionScreenInstance.GetParent().RemoveChild(_gameLoadTransitionScreenInstance);
        }

        // _ShowUI();
    }

    public async Task CreateTimer(float milliseconds)
    {
        float seconds = milliseconds / 1000f;
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }
}
