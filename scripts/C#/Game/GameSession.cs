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
    
    
}
