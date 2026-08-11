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

    /// <summary>The active full-screen loading overlay (LoadingScreen.png). Lives on this autoload so
    /// it survives the scene change into Game.tscn and renders above everything.</summary>
    private CanvasLayer _loadingScreenInstance;

    public static GameManager Instance { get; private set; }

    public GameSession GameSession;

    private List<PlayerScene> playerStates = new List<PlayerScene>();
    private List<PlayerFactionAssignment> _pendingPlayerFactionAssignments;
    private bool _gameInitialized = false;

    private const string ScenarioDirectory = "res://assets/data/scenarios/";

    /// <summary>Scenario data file to load when the game starts. Set before navigating to Game.tscn.</summary>
    public static string PendingScenarioPath { get; set; } = "res://assets/data/scenarios/Scenario_Basic.json";

    /// <summary>
    /// RNG seed chosen in the menus for the next game, or null to roll a fresh one at session start.
    /// Read by the host only (MultiplayerSession.StartNew), which then tells every peer — a client's
    /// own menu state never reaches its game. A CLI `seed=` argument still wins, so headless replays
    /// are unaffected by whatever the menu last held.
    /// </summary>
    public static int? PendingSeed { get; set; } = null;

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
        Instance = this;
        DebugUtilities.PrintPeerFinest("GameManager Ready");
        LoadAvailableScenarios();

        // Listen for scene changes
        GetTree().NodeAdded += OnNodeAdded;
    }

    public override void _ExitTree()
    {
        GetTree().NodeAdded -= OnNodeAdded;
        if (Instance == this)
            Instance = null;
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
                // Report rather than log the message alone: a malformed scenario file is a real
                // authoring error and the stack trace says which JSON element failed.
                ErrorReporter.ReportLocalOnly(e, $"Loading scenario file {path}");
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

    // ── Loading screen overlay (LoadingScreen.png) ──────────────────────────────
    // Raised before entering Game.tscn and dropped only once this peer has applied every setup
    // ChangeEvent, so the build churn is never visible. Owned by this autoload so it persists across
    // the scene change (the old cover lived on PlayerScene, which is spawned too late to cover its
    // own creation) and renders above everything.

    public void ShowLoadingScreen()
    {
        if (GameContext.IsHeadless) return;         // no display — a CLI run must not build the overlay
        if (_loadingScreenInstance != null) return; // idempotent

        _loadingScreenInstance = GameLoadTransitionScene.Instantiate<CanvasLayer>();
        AddChild(_loadingScreenInstance);
        DebugUtilities.PrintPeerFinest("Loading screen shown");
    }

    public void HideLoadingScreen()
    {
        if (_loadingScreenInstance == null) return;

        CanvasLayer overlay = _loadingScreenInstance;
        _loadingScreenInstance = null; // cleared before the tween, so a re-entrant Hide is a no-op

        Control cover = overlay.GetNodeOrNull<Control>("Cover");
        if (cover == null)
        {
            DebugUtilities.PrintPeerError("Loading screen has no Cover node — freeing without a fade");
            overlay.QueueFree();
            return;
        }

        Tween tween = CreateTween();
        tween.TweenProperty(cover, "modulate:a", 0.0, GameSettings.DurationMediumSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.Finished += () => overlay.QueueFree();
        DebugUtilities.PrintPeerFinest("Loading screen fading out");
    }
}
