using Godot;
using System;
using System.Collections.Generic;
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
        "InputOptionsList"
    };
    

    public override void _Ready()
    {
        DebugUtilities.PrintPeer("GameManager Ready");
        Dictionary<string, object> args = DebugUtilities.CommandLineArguments;
        DebugUtilities.PrintPeer(JsonSerializer.Serialize(args));

        if (args.ContainsKey(LocalMultiplayerGameArg) && (bool)args[LocalMultiplayerGameArg] == true)
        {
            DebugUtilities.PrintPeerError("Multiplayer not supported yet");
        }
        else
        {
            LoadSinglePlayerGame();
        }
    }

    private void LoadSinglePlayerGame()
    {
        DebugUtilities.PrintPeer("Starting single player game");
        PlayerScene playerInstance = PlayerScene.Instantiate<PlayerScene>();
        NodeUtilities.Instance.PlayersNode.AddChild(playerInstance);
        playerStates.Add(playerInstance);

        EventBus.Instance.UserInterfaceLoaded += elementName =>
        {
            DebugUtilities.PrintPeer("UserInterfaceLoaded");
            UserInterfaceElementsLoaded.Add(elementName);
            if (UserInterfaceElementsLoaded.Count == UserInterfaceElementsToLoad.Count)
            {
                SetupGameSession();
            }
        };
        
    }

    private void SetupGameSession()
    {
        DebugUtilities.PrintPeer("_setup_game_mode");
        GameSession = new GameSession();
        GameSession.StartSession(playerStates);
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

    private void ShowUI()
    {
        DebugUtilities.PrintPeer("Show UI");
        var uiInstance = UIScene.Instantiate<Control>();
        uiInstance.GetNode("FactionHandDisplay").Set("faction", "GERMANY");
        NodeUtilities.Instance.UserInterface.AddChild(uiInstance);
    }

    public async Task CreateTimer(float milliseconds)
    {
        float seconds = milliseconds / 1000f;
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }
}
