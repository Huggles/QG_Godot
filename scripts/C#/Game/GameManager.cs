using Godot;
using System;
using System.Collections.Generic;
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

    public Camera2D MyCamera => GetViewport().GetCamera2D();
    public InputManager MyInputManager => playerStates.Count > 0 ? playerStates[0].InputManager : null;
    
    public List<string> UserInterfaceElementsLoaded = new List<string>();
    public List<string> UserInterfaceElementsToLoad = new List<string>
    {
        "PlayerActionLabel",
        "InputOptionsList",
        "FactionHandDisplay",
        "FactionsContainer",
        "PresentationModal"
    };
    

    public override void _Ready()
    {
        DebugUtilities.PrintPeer("GameManager Ready");
        
        // Check if we're in the lobby scene - if so, don't start the game yet
        string currentScene = GetTree().CurrentScene?.SceneFilePath ?? "";
        if (currentScene.Contains("MultiplayerLobby"))
        {
            DebugUtilities.PrintPeer("In lobby scene - waiting for game start");
            return;
        }
        
        // If we're in the Game scene, start the game
        if (currentScene.Contains("Game.tscn"))
        {
            DebugUtilities.PrintPeer("In game scene - initializing game");
            InitializeGame();
        }
    }

    /// <summary>
    /// Called when starting the actual game (after lobby)
    /// </summary>
    public void InitializeGame()
    {
        DebugUtilities.PrintPeer("Initializing game from multiplayer");
        
        // Initialize NodeUtilities to find Game scene nodes
        NodeUtilities.Instance.InitializeGameNodes();
        
        // Determine multiplayer mode based on connected peers
        var peerIds = Multiplayer.GetPeers();
        int totalPlayers = peerIds.Length + 1; // +1 for ourselves
        
        DebugUtilities.PrintPeer($"Total players: {totalPlayers}");
        
        if (totalPlayers == 1)
        {
            // Single player
            LoadSinglePlayerGame();
        }
        else if (totalPlayers == 2)
        {
            // 2-player teams
            LoadTwoPlayerTeamsGame();
        }
        else if (totalPlayers >= 6)
        {
            // 6-player
            LoadSixPlayerGame();
        }
        else
        {
            // Default to single player for now
            DebugUtilities.PrintPeerError($"Unsupported player count: {totalPlayers}. Defaulting to single player.");
            LoadSinglePlayerGame();
        }
    }

    private void LoadSinglePlayerGame()
    {
        DebugUtilities.PrintPeer("Starting single player game");
        
        // Create and register player
        PlayerScene playerInstance = PlayerScene.Instantiate<PlayerScene>();
        playerInstance.PeerId = 1;
        playerInstance.PlayerName = "Player 1";
        NodeUtilities.Instance.PlayersNode.AddChild(playerInstance);
        playerStates.Add(playerInstance);
        
        // Register player in registry
        PlayerFactionRegistry.RegisterPlayer(playerInstance);
        
        // Assign all factions to single player
        PlayerFactionRegistry.AssignFactionsToPlayer(1, StaticGameData.PlayableFactions);
        PlayerFactionRegistry.PrintStatus();

        EventBus.Instance.UserInterfaceLoaded += elementName =>
        {             
            DebugUtilities.PrintPeer("UserInterfaceLoaded");
            UserInterfaceElementsLoaded.Add(elementName);
            bool areEqual = new HashSet<string>(UserInterfaceElementsLoaded).SetEquals(UserInterfaceElementsToLoad);
            if (areEqual)
            {
                DebugUtilities.PrintPeer("LoadUI");
                LoadUI();

                DebugUtilities.PrintPeer("SetupGameSession");
                SetupGameSession();
            }
        };        
    }

    private void LoadMultiplayerGame(MultiplayerMode mode)
    {
        DebugUtilities.PrintPeer($"Starting multiplayer game: {mode}");
        
        switch (mode)
        {
            case MultiplayerMode.TWO_PLAYER_TEAMS:
                LoadTwoPlayerTeamsGame();
                break;
                
            case MultiplayerMode.SIX_PLAYER:
                LoadSixPlayerGame();
                break;
                
            default:
                DebugUtilities.PrintPeerError($"Unsupported multiplayer mode: {mode}");
                LoadSinglePlayerGame();
                break;
        }
    }

    private void LoadTwoPlayerTeamsGame()
    {
        DebugUtilities.PrintPeer("Loading 2-player teams game (AXIS vs ALLIES)");
        
        // Get connected peers
        int myPeerId = Multiplayer.GetUniqueId();
        var peerIds = new List<int>(Multiplayer.GetPeers()) { myPeerId };
        peerIds.Sort(); // Ensure consistent ordering
        
        DebugUtilities.PrintPeer($"Creating players for peer IDs: {string.Join(", ", peerIds)}");
        
        // Create player 1 (AXIS) - first peer ID
        PlayerScene player1 = PlayerScene.Instantiate<PlayerScene>();
        player1.PeerId = peerIds[0];
        player1.PlayerName = $"Player {peerIds[0]} (AXIS)";
        NodeUtilities.Instance.PlayersNode.AddChild(player1);
        playerStates.Add(player1);
        PlayerFactionRegistry.RegisterPlayer(player1);
        
        // Create player 2 (ALLIES) - second peer ID
        PlayerScene player2 = PlayerScene.Instantiate<PlayerScene>();
        player2.PeerId = peerIds[1];
        player2.PlayerName = $"Player {peerIds[1]} (ALLIES)";
        NodeUtilities.Instance.PlayersNode.AddChild(player2);
        playerStates.Add(player2);
        PlayerFactionRegistry.RegisterPlayer(player2);
        
        // Step 2: Assign factions to players
        List<Faction> axisFactions = new List<Faction> { Faction.GERMANY, Faction.JAPAN, Faction.ITALY };
        List<Faction> alliesFactions = new List<Faction> { Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES };
        
        PlayerFactionRegistry.AssignFactionsToPlayer(peerIds[0], axisFactions);
        PlayerFactionRegistry.AssignFactionsToPlayer(peerIds[1], alliesFactions);
        PlayerFactionRegistry.PrintStatus();
        
        EventBus.Instance.UserInterfaceLoaded += elementName =>
        {             
            DebugUtilities.PrintPeer("UserInterfaceLoaded");
            UserInterfaceElementsLoaded.Add(elementName);
            bool areEqual = new HashSet<string>(UserInterfaceElementsLoaded).SetEquals(UserInterfaceElementsToLoad);
            if (areEqual)
            {
                DebugUtilities.PrintPeer("LoadUI");
                LoadUI();

                DebugUtilities.PrintPeer("SetupGameSession");
                SetupGameSession();
            }
        };
    }

    private void LoadSixPlayerGame()
    {
        DebugUtilities.PrintPeer("Loading 6-player game");
        
        // Step 1: Create and register all players
        int peerId = 1;
        foreach (Faction faction in StaticGameData.PlayableFactions)
        {
            PlayerScene player = PlayerScene.Instantiate<PlayerScene>();
            player.PeerId = peerId;
            player.PlayerName = $"{faction} Player";
            NodeUtilities.Instance.PlayersNode.AddChild(player);
            playerStates.Add(player);
            PlayerFactionRegistry.RegisterPlayer(player);
            
            peerId++;
        }
        
        // Step 2: Assign one faction to each player
        peerId = 1;
        foreach (Faction faction in StaticGameData.PlayableFactions)
        {
            PlayerFactionRegistry.AssignFactionsToPlayer(peerId, new List<Faction> { faction });
            peerId++;
        }
        PlayerFactionRegistry.PrintStatus();
        
        EventBus.Instance.UserInterfaceLoaded += elementName =>
        {             
            DebugUtilities.PrintPeer("UserInterfaceLoaded");
            UserInterfaceElementsLoaded.Add(elementName);
            bool areEqual = new HashSet<string>(UserInterfaceElementsLoaded).SetEquals(UserInterfaceElementsToLoad);
            if (areEqual)
            {
                DebugUtilities.PrintPeer("LoadUI");
                LoadUI();

                DebugUtilities.PrintPeer("SetupGameSession");
                SetupGameSession();
            }
        };
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
