using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameSession : Node
{
    private static GameSession instance;
    public static GameSession Instance {
        get {
            if (instance == null) {
                throw new System.ArgumentNullException("GameSession Not Initialized");
            }
            return instance;
        }
    }

    private List<PlayerScene> playerScenes { get; set; }

    public IGameMode GameMode;
    [Export] public GameState GameState;
    [Export] public GameFlow GameFlow;

    public GameSession(){
        instance = this;        
    }

    public void StartSession(List<PlayerScene> playerScenes){
        DebugUtilities.PrintPeer("Start Session");
        this.playerScenes = playerScenes;

        GameState = new GameState();
        GameMode = new GameModeDefault();
        GameMode.Init();

        RegisterGameEvents();

        GameFlow = new GameFlow();
        GameFlow.StartGame();

        OnGameStarted();
    }



    private void RegisterGameEvents(){
        EventBus.Instance.RecalculateSupply += RecalculateSupply;
        EventBus.Instance.RecalculateStraights += RecalculateStraights;
    }
    private void OnGameStarted(){
        
    }

    /**
    * API
    */
    public static Dictionary<Faction, FactionState> FactionStates => instance.GameState.FactionStates;
    public static List<StraightState> StraightStates => instance.GameState.StraightStates;

    public static Dictionary<string, CountryState> CountryStatesByName => instance.GameState.CountryStateByName;
    public static Dictionary<int, CountryState> CountryStatesById => instance.GameState.CountryStateById;

    public static Dictionary<int, UnitState> UnitStatesById => instance.GameState.UnitStatesById;

    public static void RecalculateSupply()
    {
        foreach (Faction faction in Enum.GetValues(typeof(Faction))) {
            RecalculateSupplyForFaction(faction);
        }            
    }
    private static void RecalculateSupplyForFaction(Faction faction)
    {
        var factionState = FactionStates[faction];
        var pathFindingService = new PathFindingService(new PathFindingNodeDefault(), faction);
        List<UnitState> activeUnits = UnitState.ForIds(factionState.ActiveUnitIds);
        IEnumerable<UnitState> armies = activeUnits.Where(u => u.Type == UnitType.ARMY);
        IEnumerable<UnitState> navies = activeUnits.Where(u => u.Type == UnitType.NAVY);

        foreach (UnitState unit in armies) {
            bool isSupplied = RecalculateSupplyForUnit(pathFindingService, unit.Id);
            if(isSupplied){
                unit.SetInSupply();
            }else {
                unit.SetOutOfSupply();
            }
        }
        foreach (UnitState unit in navies) {
            bool isSupplied = RecalculateSupplyForUnit(pathFindingService, unit.Id);
            if(isSupplied){
                unit.SetInSupply();
            }else {
                unit.SetOutOfSupply();
            }
        }
    }
    private static bool RecalculateSupplyForUnit(PathFindingService pathFinding, int unitId)
    {
        var unit = UnitState.ForId(unitId);
        foreach (var supplyId in GameStateUtilities.GetSupplyCountryIds(unit.Faction))
        {
            if (pathFinding.CalculatePath(unit.CountryId, supplyId))
            {
                if (unit.IsNavy)
                    return unit.CountryState.HasHarbor(unit.Faction);
                return true;
            }
        }

        DebugUtilities.PrintPeer($"Unit is now out of supply in {unit.CountryState.Label} ({unit.Faction})");
        return false;
    }
    public static void RecalculateStraights()
    {
        foreach (StraightState straightState in StraightStates)
            straightState.RecalculateControlledBy();
    }
    public static void DeployUnitToCountry(int countryId, Faction faction, UnitType unitType, DeployType deployType)
    {
        if (countryId == -1) {
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

        
        //unit.BeforeUnitRemovedFromCountry.Emit(unitId, country.Id);
        country.Units.Remove(unit.Faction);
        unit.CountryId = -1;
        //unit.AfterUnitRemovedFromCountry.Emit();
    }
    public static void AttackUnit(int unitId)
    {
        if (unitId == -1)
            return;

        var unit = UnitStatesById[unitId];
        int countryId = unit.CountryId;

        //unit.BeforeUnitRemovedFromCountry.Emit(unitId, countryId);
        unit.CountryState.Units.Remove(unit.Faction);
        unit.CountryId = -1;
        //unit.AfterUnitRemovedFromCountry.Emit();
    }
    
    public static List<int> BuildableCountriesForFaction(Faction faction)
    {
        var response = new List<int>();
        var suppliedUnitIds = GameStateUtilities.SuppliedUnitsForFaction(faction);

        if (suppliedUnitIds.Count > 0)
        {
            foreach (var country in CountryState.ForUnitIds(suppliedUnitIds))
            {
                foreach (var neighbor in country.NeighborCountryStates)
                {
                    if (neighbor.CanBuild(faction))
                        response.Add(neighbor.Id);
                }
            }
        }
        return response;
    }
    public static List<int> BuildableLandCountriesForFaction(Faction faction) {
        return BuildableCountriesForFaction(faction).Where(id => CountryState.ForId(id).Type == CountryType.LAND).ToList();
    }   
    public static List<int> BuildableSeaCountriesForFaction(Faction faction) {
        return BuildableCountriesForFaction(faction).Where(id => CountryState.ForId(id).Type == CountryType.SEA).ToList();
    }

    public static void EliminateUnit(string unitId) { }
    public static void ScoreVictoryPoints(Faction faction, int vp) => FactionStates[faction].Score += vp;
    public static void HandDiscard(List<string> cardIds) { }
    public static void ForceDiscard(string cardId) { }
    public static void ForceDrawDeckDiscard(string faction, int numberOfCards) { }
    public static void PlayCard(string cardId) { }
    public static void ActivateStatusCard(string cardId) { }
    public static void ActivateResponseCard(string cardId) { }

    public async static Task<CardState> RequestCardPlay(Faction faction) {
        //TODO: determine player scene for faction.
        instance.playerScenes[0].InputManager.SetPlayCardInputActive(CardPlayPool.GetNextActions(faction));
        Variant[] results = await EventBus.GetSignalAwaiter("CardSelected");

        CardActivationOption cardActivationOption = results[0].As<CardActivationOption>();        
        CardState cardState = CardState.ForId(cardActivationOption.CardId);
        
        return cardState;
    }
}
