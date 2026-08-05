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

    private const string ScenarioDirectory = "res://assets/data/scenarios/";

    /// <summary>Scenario data file to load when the game starts. Set before navigating to Game.tscn.</summary>
    public static string PendingScenarioPath { get; set; } = "res://assets/data/scenarios/Scenario_Basic.json";

    public List<ScenarioInfo> AvailableScenarios { get; private set; } = new();
    public ScenarioInfo SelectedScenario { get; private set; }

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
        DebugUtilities.PrintPeerFinest("GameManager Ready");
        LoadAvailableScenarios();
        
        // Listen for scene changes
        GetTree().NodeAdded += OnNodeAdded;
    }

    public override void _ExitTree()
    {
        GetTree().NodeAdded -= OnNodeAdded;
    }

    private void LoadAvailableScenarios()
    {
        using DirAccess dir = DirAccess.Open(ScenarioDirectory);
        if (dir == null)
        {
            DebugUtilities.PrintPeerError($"GameManager: failed to open scenario directory {ScenarioDirectory}");
            return;
        }

        dir.ListDirBegin();
        string fileName;
        while ((fileName = dir.GetNext()) != "")
        {
            if (dir.CurrentIsDir())
                continue;

            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            string path = $"{ScenarioDirectory}{fileName}";
            try
            {
                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                string json = file.GetAsText();
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                var info = new ScenarioInfo
                {
                    Path = path,
                    Name = System.IO.Path.GetFileNameWithoutExtension(fileName)
                };

                if (root.TryGetProperty("title", out JsonElement titleElement) && titleElement.ValueKind == JsonValueKind.String)
                    info.Title = titleElement.GetString();

                if (root.TryGetProperty("description", out JsonElement descriptionElement) && descriptionElement.ValueKind == JsonValueKind.String)
                    info.Description = descriptionElement.GetString();

                if (string.IsNullOrEmpty(info.Title))
                    info.Title = info.Name.Replace('_', ' ');
                if (string.IsNullOrEmpty(info.Description))
                    info.Description = "No description available.";

                AvailableScenarios.Add(info);
                DebugUtilities.PrintPeerFinest($"GameManager: loaded scenario '{info.Title}' from {path}");
            }
            catch (Exception e)
            {
                DebugUtilities.PrintPeerError($"GameManager: failed to load scenario file {path}: {e.Message}");
            }
        }

        dir.ListDirEnd();
        AvailableScenarios = AvailableScenarios.OrderBy(s => s.Name).ToList();
        SelectedScenario = AvailableScenarios.FirstOrDefault(s => s.Name.Equals("Scenario_Basic", StringComparison.OrdinalIgnoreCase))
                           ?? AvailableScenarios.FirstOrDefault();

        if (SelectedScenario != null)
            PendingScenarioPath = SelectedScenario.Path;
    }

    public void SetSelectedScenarioByIndex(int index)
    {
        if (index < 0 || index >= AvailableScenarios.Count)
            return;

        SelectedScenario = AvailableScenarios[index];
        PendingScenarioPath = SelectedScenario.Path;
    }

    private void OnNodeAdded(Node node)
    {
        // When the Game scene root is added, check for pending player-faction assignments
        if (node.Name == "Game" && node.SceneFilePath == "res://scenes/Game.tscn")
        {
            DebugUtilities.PrintPeerFinest("Game scene detected");
            node.GetNode<PeerReadinessComponent>("PeerReadinessComponent").AllPeersReady += () =>
            {
                DebugUtilities.PrintPeerFinest("All peers ready in Game scene - initializing game");
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
        DebugUtilities.PrintPeerFinest("Multiplayer session started - waiting for clients to initialize");
        await NetworkApi.Instance.StartMultiplayerSession(JsonSerializer.Serialize(playerFactionAssignments));
        DebugUtilities.PrintPeerFinest("Multiplayer session initialization complete - waiting for UI elements to load");

        MultiplayerSession.Instance.StartNew(JsonSerializer.Serialize(playerFactionAssignments));
    }
}
