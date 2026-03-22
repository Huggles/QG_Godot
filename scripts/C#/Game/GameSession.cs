using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameSession : Node
{
    private static GameSession instance;
    public static GameSession Instance
    {
        get
        {
            if (instance == null)
            {
                throw new System.ArgumentNullException("GameSession Not Initialized");
            }
            return instance;
        }
    }

    public static bool IsStarted = false;

    private List<PlayerScene> playerScenes { get; set; }

    public IGameMode GameMode;
    [Export] public GameState GameState;
    [Export] public GameFlow GameFlow;

    public GameSession()
    {
        instance = this;
    }

    public async Task StartSession(List<PlayerScene> playerScenes)
    {
        DebugUtilities.PrintPeer("Start Session");
        this.playerScenes = playerScenes;

        GameState = new GameState();
        GameMode = new GameModeDefault();
        GameFlow = new GameFlow();
        await GameMode.Init();

        GameFlow.StartGame();

        OnGameStarted();
    }

    private void OnGameStarted()
    {
        IsStarted = true;
        EventBus.Emit(EventBus.SignalName.GameSessionStarted);
    }

    /**
    * API
    */
    public static Dictionary<Faction, FactionState> FactionStates => instance.GameState.FactionStates;
    public static List<StraightState> StraightStates => instance.GameState.StraightStates;

    public static Dictionary<string, CountryState> CountryStatesByName => instance.GameState.CountryStateByName;
    public static Dictionary<int, CountryState> CountryStatesById => instance.GameState.CountryStateById;

    public static Dictionary<int, UnitState> UnitStatesById => instance.GameState.UnitStatesById;

    public static void DeployUnitToCountry(int countryId, Faction faction, UnitType unitType, DeployType deployType)
    {
        if (countryId == -1)
        {
            DebugUtilities.PrintPeerError("Tried to deploy to null country: " + countryId);
            return;
        }


        int unitId = UnitPool.GetAvailableUnitForFaction(faction, unitType);
        var unit = UnitState.ForId(unitId);
        var country = CountryStatesById[countryId];
        bool deployable = deployType != DeployType.BUILD || country.CanBuild(faction);

        if (!country.IsCountryFull && deployable)
        {

            unit.EmitSignal("BeforeUnitDeployedToCountry");
            country.Units[unit.Faction] = unit.Id;
            unit.CountryId = countryId;
            unit.EmitSignal("AfterUnitDeployedToCountry");
        }
    }
    public static void RemoveUnitFromCountry(int unitId)
    {
        if (unitId == -1)
            return;

        UnitState unit = UnitState.ForId(unitId);
        CountryState country = CountryState.ForId(unit.CountryId);


        unit.EmitSignal(UnitState.SignalName.BeforeUnitRemovedFromCountry, unitId, country.Id);
        country.Units.Remove(unit.Faction);
        unit.CountryId = -1;
        unit.EmitSignal(UnitState.SignalName.AfterUnitRemovedFromCountry, unitId, country.Id);
    }
    public static void AttackUnit(int unitId)
    {
        if (unitId == -1)
            return;

        var unit = UnitStatesById[unitId];
        int countryId = unit.CountryId;

        unit.EmitSignal(UnitState.SignalName.BeforeUnitRemovedFromCountry, unitId, countryId);
        unit.CountryState.Units.Remove(unit.Faction);
        unit.CountryId = -1;
        unit.EmitSignal(UnitState.SignalName.AfterUnitRemovedFromCountry, unitId, countryId);
    }


    public static void EliminateUnit(string unitId) { }
    public static void ScoreVictoryPoints(Faction faction, int vp) => FactionStates[faction].Score += vp;
    public static void HandDiscard(List<string> cardIds) { }
    public static void ForceDiscard(string cardId) { }
    public static void ForceDrawDeckDiscard(string faction, int numberOfCards) { }
    public static void PlayCard(string cardId) { }
    public static void ActivateStatusCard(string cardId) { }
    public static void ActivateResponseCard(string cardId) { }

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
