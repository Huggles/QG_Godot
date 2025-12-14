using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public partial class GameState : StateObject
{
    public static GameQueue<GameAnimation> AnimationQueue = new GameQueue<GameAnimation>();
    public IGameMode GameMode;
    public Dictionary<Faction, FactionState> FactionStates = [];
    public List<ChangeEvent> GameChangeEvents = [];
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


    public Dictionary<int, CardStep> CardStepsById = new();
   

    
}
