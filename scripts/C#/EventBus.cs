using Godot;
using System;

public partial class EventBus : GodotObject
{
    private static EventBus instance;
    private static EventBus Instance {
        get {
            if (instance == null){
                instance = new EventBus();
            }
            return instance;
        }
    }

    
    public static void Emit(String name){
        Instance.EmitSignal(name);        
    }
    public static void Emit(String name, params Variant[] args){
        Instance.EmitSignal(name, args);        
    }
    public static SignalAwaiter GetSignalAwaiter(String name){
        return Instance.ToSignal(EventBus.Instance,name);        
    }


    [Signal] public delegate void PlayerJoinedEventHandler();
    [Signal] public delegate void PlayerLeftEventHandler();

    [Signal] public delegate void CountryClickedEventHandler();
    [Signal] public delegate void UnitClickedEventHandler();

    [Signal] public delegate void SetCountriesClickableEventHandler();
    [Signal] public delegate void SetAllCountriesUnclickableEventHandler();

    [Signal] public delegate void SetUnitsClickableEventHandler();
    [Signal] public delegate void SetAllUnitsUnclickableEventHandler();

    [Signal] public delegate void GameChangeEventOccurredEventHandler();

    [Signal] public delegate void CardSelectedEventHandler();

    [Signal] public delegate void RequestStatusCardEventHandler();
    [Signal] public delegate void RequestResponseCardEventHandler();

    [Signal] public delegate void CardPlayStartedEventHandler();
    [Signal] public delegate void CardPlayCompletedEventHandler();

    [Signal] public delegate void CardPlayHandlerCompletedEventHandler();

    [Signal] public delegate void NoStatusCardActivatedEventHandler();
    [Signal] public delegate void StatusCardActivationStartedEventHandler();
    [Signal] public delegate void StatusCardActivationCompletedEventHandler();

    [Signal] public delegate void NoResponseCardActivatedEventHandler();
    [Signal] public delegate void ResponseCardActivationStartedEventHandler();
    [Signal] public delegate void ResponseCardActivationCompletedEventHandler();

    [Signal] public delegate void GameChangeEventBeforeEventHandler();
    [Signal] public delegate void GameChangeEventAfterEventHandler();

    [Signal] public delegate void NewTurnStartedEventHandler();
    [Signal] public delegate void NextStepStartedEventHandler();

    [Signal] public delegate void FactionScoredPointsEventHandler();

    [Signal] public delegate void RecalculateSupplyEventHandler();
    [Signal] public delegate void RecalculateStraightsEventHandler();

    [Signal] public delegate void VpDetailsPanelOpenedEventHandler();
}
