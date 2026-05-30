using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class FactionState : StateObject
{
    public FactionData FactionData { get; private set; }
    private int _score { get; set; }


    public Faction Faction => FactionData.Faction;

    public string FactionLabel => Faction.ToString();

    private MultiplayerGameState GameState => GameSession.Current.GameState;


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

    public List<int> AllUnits
    {
        get
        {
            var response = new List<int>();
            foreach (UnitState unitState in GameState.UnitStates)
            {
                if (unitState.Faction == Faction)
                    response.Add(unitState.Id);
            }
            return response;
        }
    }

    public List<int> ActiveUnitIds
    {
        get
        {
            return UnitState.ForIds(AllUnits)
                .Where(unit => unit.CountryId >= 0)
                .Select(unit => unit.Id)
                .ToList();
        }
    }

    public List<int> OccupiedCountryIds
    {
        get
        {
            return ActiveUnitIds
                .Select(id => GameState.UnitStatesById[id].CountryId)
                .ToList();
        }
    }

    public List<int> SuppliedUnitIds
    {
        get
        {
            return ActiveUnitIds
                .Where(id => GameState.UnitStatesById[id].InSupply)
                .ToList();
        }
    }

    public List<int> UnsuppliedUnitIds
    {
        get
        {
            return ActiveUnitIds
                .Where(id => !GameState.UnitStatesById[id].InSupply)
                .ToList();
        }
    }

    public static FactionState ForEnum(Faction factionEnum)
    {
        
        return MultiplayerSession.Instance.GameState.FactionStates[factionEnum];
    }
}
