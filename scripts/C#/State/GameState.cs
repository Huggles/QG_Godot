using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public partial class GameState : StateObject
{
    public IGameMode GameMode;
    public Dictionary<Faction, FactionState> FactionStates = new();
    public List<ChangeEvent> GameChangeEvents = new();    
    public CardState ActivePlayerCard;

    private int _changeEventCounter = 0;

    [Signal] public delegate void CountryClickedEventHandler();

    public List<CountryState> CountryStates = new();
    private Dictionary<int, CountryState> _countryStateById = new();
    public Dictionary<int, CountryState> CountryStateById
    {
        get
        {
            if (_countryStateById.Count == 0)
                foreach (var cs in CountryStates)
                    _countryStateById[cs.Id] = cs;
            return _countryStateById;
        }
    }

    private Dictionary<string, CountryState> _countryStateByName = new();
    public Dictionary<string, CountryState> CountryStateByName
    {
        get
        {
            if (_countryStateByName.Count == 0)
                foreach (var cs in CountryStates)
                    _countryStateByName[cs.Name] = cs;
            return _countryStateByName;
        }
    }

    public List<StraightState> StraightStates = new();
    private Dictionary<int, StraightState> _straightStateByControllingCountryId = new();
    public Dictionary<int, StraightState> StraightStateByControllingCountryId
    {
        get
        {
            if (_straightStateByControllingCountryId.Count == 0)
                foreach (var ss in StraightStates)
                    _straightStateByControllingCountryId[ss.ControllingCountryId] = ss;
            return _straightStateByControllingCountryId;
        }
    }

    public List<UnitState> UnitStates = new();
    private Dictionary<int, UnitState> _unitStatesById = new();
    public Dictionary<int, UnitState> UnitStatesById
    {
        get
        {
            if (_unitStatesById.Count == 0)
                foreach (var us in UnitStates)
                    _unitStatesById[us.Id] = us;
            return _unitStatesById;
        }
    }

    public List<CardState> CardStates = new();
    private Dictionary<int, CardState> _cardStatesById = new();
    public Dictionary<int, CardState> CardStatesById
    {
        get
        {
            if (_cardStatesById.Count == 0)
                foreach (var cs in CardStates)
                    _cardStatesById[cs.Id] = cs;
            return _cardStatesById;
        }
    }

    private Dictionary<string, CardState> _cardStatesByName = new();
    public Dictionary<string, CardState> CardStatesByName
    {
        get
        {
            if (_cardStatesByName.Count == 0)
                foreach (var cs in CardStates)
                    _cardStatesByName[cs.CardData.UniqueName] = cs;
            return _cardStatesByName;
        }
    }

    public void RecalculateSupply()
    {
        foreach (Faction faction in Enum.GetValues(typeof(Faction))) {
            RecalculateSupplyForFaction(faction);
        }
            
    }

    private void RecalculateSupplyForFaction(Faction faction)
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

    private bool RecalculateSupplyForUnit(PathFindingService pathFinding, int unitId)
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

    public void RecalculateStraights()
    {
        foreach (StraightState straightState in StraightStates)
            straightState.RecalculateControlledBy();
    }

    public FactionState FactionStateForEnum(Faction faction) => FactionStates[faction];

    public void DeployUnitToCountry(int countryId, Faction faction, UnitType unitType, DeployType deployType)
    {
        if (countryId == -1) {
            DebugUtilities.PrintPeerError("Tried to deploy to null country: " + countryId);
            return;
        }
            

        int unitId = UnitPool.GetAvailableUnitForFaction(faction, unitType);
        var unit = UnitState.ForId(unitId);
        var country = CountryStateById[countryId];
        bool deployable = deployType != DeployType.BUILD || country.CanBuild(faction);

        if (!country.IsCountryFull && deployable)
        { 
            
            unit.EmitSignal("BeforeUnitDeployedToCountry");
            country.Units[unit.Faction] = unit.Id;
            unit.CountryId = countryId;
            unit.EmitSignal("AfterUnitDeployedToCountry");
        }
    }

    public void RemoveUnitFromCountry(int unitId)
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

    public void AttackUnit(int unitId)
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

    public void EliminateUnit(string unitId) { }
    public void ScoreVictoryPoints(Faction faction, int vp) => FactionStates[faction].Score += vp;
    public void HandDiscard(List<string> cardIds) { }
    public void ForceDiscard(string cardId) { }
    public void ForceDrawDeckDiscard(string faction, int numberOfCards) { }
    public void PlayCard(string cardId) { }
    public void ActivateStatusCard(string cardId) { }
    public void ActivateResponseCard(string cardId) { }

    public List<int> BuildableCountriesForFaction(Faction faction)
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

    public List<int> BuildableLandCountriesForFaction(Faction faction) =>
        BuildableCountriesForFaction(faction).Where(id => CountryState.ForId(id).Type == CountryType.LAND).ToList();

    public List<int> BuildableSeaCountriesForFaction(Faction faction) =>
        BuildableCountriesForFaction(faction).Where(id => CountryState.ForId(id).Type == CountryType.SEA).ToList();

    public List<int> AttackableCountriesForFaction(Faction faction)
    {
        var result = new List<int>();
        var suppliedUnitIds = GameStateUtilities.SuppliedUnitsForFaction(faction);

        foreach (var country in CountryState.ForUnitIds(suppliedUnitIds))
        {
            foreach (var connected in country.ConnectedCountries(faction))
            {
                if (connected.CanAttackWhenEmpty(faction))
                    result.Add(connected.Id);
            }
        }

        return result;
    }

    public List<int> AttackableUnitsForFaction(Faction faction)
    {
        var result = new List<int>();
        var suppliedUnitIds = GameStateUtilities.SuppliedUnitsForFaction(faction);

        foreach (var country in CountryState.ForUnitIds(suppliedUnitIds))
        {
            foreach (var connected in country.ConnectedCountries(faction))
            {
                if (connected.OccupyingTeam == StaticGameData.OpponentFactionTeamForFaction(faction))
                    result.AddRange(connected.Units.Values);
            }
        }

        return result;
    }
}
