using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public partial class FactionState : StateObject
{
    [JsonIgnore] public FactionData FactionData { get; private set; }
    private int _score { get; set; }

    public Faction Faction => FactionData.Faction;

    public string FactionLabel => Faction.ToString();

    [JsonIgnore] private MultiplayerGameState GameState => GameSession.Current.GameState;


    public int Score
    {
        get => _score;
        set
        {
            _score = value;
            //EventBusLocal.EmitSignal(nameof(EventBusLocal.FactionScoredPoints), Faction, _score);
        }
    }

    public DeckState DeckState { get; private set; }

    public FactionState(FactionData factionData)
    {
        FactionData = factionData;
        _score = 0;
        DeckState = new DeckState(this);
    }

    public List<int> AllUnits => GameState.UnitStates.Where(unit => unit.Faction == Faction).Select(unit => unit.Id).ToList();
    public List<int> ActiveUnitIds => UnitState.ForIds(AllUnits).Where(unit => unit.CountryId >= 0).Select(unit => unit.Id).ToList();
    public List<int> OccupiedCountryIds => ActiveUnitIds.Select(id => GameState.UnitStatesById[id].CountryId).ToList();
    public List<int> SuppliedUnitIds => ActiveUnitIds.Where(id => GameState.UnitStatesById[id].InSupply).ToList();
    public List<int> UnsuppliedUnitIds => ActiveUnitIds.Where(id => !GameState.UnitStatesById[id].InSupply).ToList();

    public static FactionState ForEnum(Faction factionEnum)
    {   
        return MultiplayerSession.Instance.GameState.FactionStatesByFaction[factionEnum];
    }
}
