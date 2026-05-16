using Godot;
using Godot.Collections;
using Godot.NativeInterop;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

public partial class GameState : StateObject
{   
    public IGameMode GameMode;

    [Export]
    public Dictionary<Faction, FactionState> FactionStates = new();

    [Export]
    public Dictionary<string, int> ABC = new();
    public Array<ChangeEvent> GameChangeEvents = new();
    public CardState ActivePlayerCard;

    private int _changeEventCounter = 0;

    [Signal] public delegate void CountryClickedEventHandler();

    [Export]
    public Array<CountryState> CountryStates = new();

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

    [Export]
    public Array<StraightState> StraightStates = new();
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

    [Export]    
    public Array<UnitState> UnitStates = new();
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

    [Export]
    public Array<CardState> CardStates = new();
    public Dictionary<int, CardState> CardStatesById
    {
        get
        {            
            if (field.Count == 0)
                foreach (var cs in CardStates)
                    field[cs.Id] = cs;
            return field;
        }
    }

    private Dictionary<string, CardState> _cardStatesByName = new();
    public Dictionary<string, CardState> CardStatesByName
    {
        get
        {
            if (field.Count == 0)
                foreach (var cs in CardStates)
                    field[cs.CardData.UniqueName] = cs;
            return field;
        }
    }


    public Dictionary<int, CardStep> CardStepsById = new();

    public override void _Ready()
    {
        DebugUtilities.PrintPeer($"GameState ready: " + this.Name, DebugVerbosity.INFO);
    }

    public void OnPropertyChanged(string propertyName)
    {
        DebugUtilities.PrintPeer(propertyName);
    }
}
