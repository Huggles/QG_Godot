using Godot;
using Metalama.Patterns.Observability;
using System.Collections.Generic;
using System.Linq;

public partial class GameState : StateObject
{   
    public IGameMode GameMode { get; set; }
    public Dictionary<Faction, FactionState> FactionStates { get; set; }    
    public List<ChangeEvent> GameChangeEvents { get; set; }
    public CardState ActivePlayerCard { get; set; }

    public List<CountryState> CountryStates { get; set; }

    public Dictionary<int, CountryState> CountryStateById
    {
        get
        {
            if (field.Count == 0)
                foreach (var cs in CountryStates)
                    field[cs.Id] = cs;
            return field;
        }
    }    
    public Dictionary<string, CountryState> CountryStateByName
    {
        get
        {
            if (field.Count == 0)
                foreach (var cs in CountryStates)
                    field[cs.Name] = cs;
            return field;
        }
    }

    public List<StraightState> StraightStates { get; set; }
    public Dictionary<int, StraightState> StraightStateByControllingCountryId
    {
        get
        {
            if (field.Count == 0)
                foreach (var ss in StraightStates)
                    field[ss.ControllingCountryId] = ss;
            return field;
        }
    }

    public List<UnitState> UnitStates { get; set; }
    public Dictionary<int, UnitState> UnitStatesById
    {
        get
        {
            if (field.Count == 0)
                foreach (var us in UnitStates)
                    field[us.Id] = us;
            return field;
        }
    }

    public List<CardState> CardStates = new();
    public Dictionary<int, CardState> CardStatesById => CardStates.ToDictionary(cs => cs.Id);

    private Dictionary<string, CardState> _cardStatesByName = new();
    public Dictionary<string, CardState> CardStatesByName => CardStates.ToDictionary(cs => cs.CardData.UniqueName);

    public Dictionary<int, CardStep> CardStepsById = new();
}
