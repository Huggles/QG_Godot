using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

public partial class GameSession : Node
{    
    public static GameSession Instance
    {
        get
        {
            if (field == null)
            {
                throw new System.ArgumentNullException("GameSession Not Initialized");
            }
            return field;
        }
        private set;
    }

    public static MultiplayerSession Current
    {
        get
        {
            if (MultiplayerSession.Instance != null)
            {
                return MultiplayerSession.Instance;
            }
            if (field == null)
            {
                throw new System.ArgumentNullException("GameSession Not Initialized");
            }
            return field;
        }
        private set;
    }

    public static bool IsStarted = false;

    // Tracks the current faction on clients (set via RPC; on host computed from GameFlow)
    private static Faction _clientCurrentFaction;
    public static Faction CurrentFaction =>
        Instance?.GameFlow != null ? Instance.GameFlow.CurrentFaction : _clientCurrentFaction;

    private List<PlayerScene> playerScenes { get; set; }

    public IGameMode GameMode;
    public GameState GameState;
    [Export] public GameFlow GameFlow;
    
    public override void _EnterTree()
    {
        base._EnterTree();
        Instance = this;
    }

    public async Task StartSession(List<PlayerScene> playerScenes)
    {
        DebugUtilities.PrintPeer("Start Session", DebugVerbosity.INFO);
        this.playerScenes = playerScenes;

        

        GameState = new GameState();
        //GameMode = new GameModeDefault();
        //GameFlow = new GameFlow();
        //await GameMode.Init();

        //GameFlow.StartGame();

        //OnGameStarted();
    }

    private void OnGameStarted()
    {
        IsStarted = true;
        EventBus.Emit(EventBus.SignalName.GameSessionStarted);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveGameStarted()
    {
        DebugUtilities.PrintPeer("Client received GameStarted - fading loading screen", DebugVerbosity.INFO);
        PlayerScene player = PlayerFactionRegistry.GetPlayerSceneForFaction(
            PlayerFactionRegistry.GetLocalPlayerFactions().FirstOrDefault());
        player?.FadeLoadingScreen();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveNewTurnStarted(int turnNumber, int currentFactionId)
    {
        _clientCurrentFaction = (Faction)currentFactionId;
        DebugUtilities.PrintPeer($"Client received NewTurnStarted: turn={turnNumber}, faction={_clientCurrentFaction}", DebugVerbosity.INFO);
        EventBus.Emit(EventBus.SignalName.NewTurnStarted, turnNumber);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveNextStepStarted(int turnStep, int currentFactionId)
    {
        _clientCurrentFaction = (Faction)currentFactionId;
        DebugUtilities.PrintPeer($"Client received NextStepStarted: step={turnStep}, faction={_clientCurrentFaction}", DebugVerbosity.INFO);
        EventBus.Emit(EventBus.SignalName.NextStepStarted, turnStep);
    }

    /**
    * API
    */
    public Dictionary<Faction, FactionState> FactionStates => GameState.FactionStates.ToDictionary();
    public List<StraightState> StraightStates => GameState.StraightStates.ToList();

    public Dictionary<string, CountryState> CountryStatesByName => GameState.CountryStateByName.ToDictionary();
    public Dictionary<int, CountryState> CountryStatesById => GameState.CountryStateById.ToDictionary();

    public Dictionary<int, UnitState> UnitStatesById => GameState.UnitStatesById.ToDictionary();
    
    public async static Task<CardActivationOption> RequestPlay(Faction faction)
    {
        // Get the player who controls this faction
        PlayerScene controllingPlayer = PlayerFactionRegistry.GetPlayerSceneForFaction(faction);
        if (controllingPlayer == null)
        {
            DebugUtilities.PrintPeerError($"RequestPlay: No player found controlling {faction}");
            return null;
        }

        List<CardActivationOption> activationOptions = CardPlayPool.GetNextActions(faction);
        
        // Route input to the correct player's InputManager
        controllingPlayer.InputManager.SetPlayCardInputActive(activationOptions);
        
        Variant[] results = await EventBus.GetSignalAwaiter("CardSelected");
        if (results == null || results.Length == 0)
        {
            return null;
        }
        else
        {
            return results[0].As<CardActivationOption>();
        }
    }
    public async static Task<CardActivationOption> RequestBlock(Faction faction)
    {
        // Get the player who controls this faction
        PlayerScene controllingPlayer = PlayerFactionRegistry.GetPlayerSceneForFaction(faction);
        if (controllingPlayer == null)
        {
            DebugUtilities.PrintPeerError($"RequestBlock: No player found controlling {faction}");
            return null;
        }

        List<CardActivationOption> blockOptions = await CardPlayPool.BlockChangeEvents(faction);
        if (blockOptions == null || blockOptions.Count == 0)
        {
            return null;
        }
        else
        {
            // Route input to the correct player's InputManager
            controllingPlayer.InputManager.SetPlayCardInputActive(blockOptions);
            
            Variant[] results = await EventBus.GetSignalAwaiter("CardSelected");
            if (results == null || results.Length == 0)
            {
                return null;
            }
            else
            {
                return results[0].As<CardActivationOption>();
            }
        }

    }
}
