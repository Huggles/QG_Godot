using Godot;
using System;
using System.Collections.Generic;

public partial class EventBus : GodotObject
{
    private static EventBus instance;
    public static EventBus Instance {
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

    [Signal] public delegate void UserInterfaceReadyEventHandler();


    [Signal] public delegate void PlayerJoinedEventHandler();
    [Signal] public delegate void PlayerLeftEventHandler();
    [Signal] public delegate void GameSessionStartedEventHandler();

    [Signal] public delegate void FactionsAssignedEventHandler();

    [Signal] public delegate void CountryClickedEventHandler(int countryClicked);
    [Signal] public delegate void UnitClickedEventHandler(int unitClicked);

    [Signal] public delegate void GameChangeEventOccurredEventHandler();

    [Signal] public delegate void CardSelectedEventHandler();

    [Signal] public delegate void CardsDrawnEventHandler(int faction, int numberOfCards);
    [Signal] public delegate void CardsDiscardedEventHandler(int faction, int numberOfCards);

    [Signal] public delegate void InputRequestResponseReceivedEventHandler(string jsonDto);

    [Signal] public delegate void RequestStatusCardEventHandler();
    [Signal] public delegate void RequestResponseCardEventHandler();


    [Signal] public delegate void CardPlayPoolFinishedEventHandler();
    [Signal] public delegate void CardPlayStartedEventHandler();
    [Signal] public delegate void CardPlayCompletedEventHandler();
    [Signal] public delegate void CardStepFinishedEventHandler(int cardStepId);

    [Signal] public delegate void CardPlayHandlerCompletedEventHandler();

    [Signal] public delegate void NoStatusCardActivatedEventHandler();
    [Signal] public delegate void StatusCardActivationStartedEventHandler();
    [Signal] public delegate void StatusCardActivationCompletedEventHandler();

    [Signal] public delegate void NoResponseCardActivatedEventHandler();
    [Signal] public delegate void ResponseCardActivationStartedEventHandler();
    [Signal] public delegate void ResponseCardActivationCompletedEventHandler();

    /// <summary>Emitted whenever any part of GameState changes. senderType is the class name of the
    /// object that changed (e.g. "Country", "Unit"). propertyName is the changed property.
    /// An empty senderType means the whole GameState was replaced (client deserialization).
    /// UI nodes should re-read from MultiplayerSession.GameState and refresh their display.</summary>
    [Signal] public delegate void GameStateChangedEventHandler(string senderType, string propertyName);

    [Signal] public delegate void GameChangeEventBeforeEventHandler();
    /// <summary>changeEventType is the class name of the ChangeEvent that was applied (e.g. "DeployUnitChangeEvent").
    /// On the client in multiplayer it will be the type synced from the server. UI reads GameState for details.</summary>
    [Signal] public delegate void GameChangeEventAfterEventHandler(string changeEventName);

    [Signal] public delegate void NewTurnStartedEventHandler(int turnNumber);
    [Signal] public delegate void NextStepStartedEventHandler(int turnStep);

    [Signal] public delegate void FactionScoredPointsEventHandler(Faction faction, int points);

    [Signal] public delegate void UnitDeployedEventHandler(int unitId, int countryId);
    [Signal] public delegate void UnitRemovedEventHandler(int unitId, int countryId);

    [Signal] public delegate void GameStateRecalculatedEventHandler();
}
