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
        DebugUtilities.PrintPeerFinest("Start Session");
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

        Variant[] response = await NetworkApi.Instance.SendInputRequest(new InputRequest.HandCardPlayRequestHandler(faction, activationOptions.Select(opt => opt.StepId).ToList()));
        InputRequest responseDto = InputRequest.FromJson(response[0].AsString()); 
        return activationOptions.Find(opt => opt.StepId == responseDto.ResponseStepIds[0]);
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
                return new CardActivationOption((int)results[0]);
            }
        }

    }
}
