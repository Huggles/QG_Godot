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


    [Signal] public delegate void CountryNamesToggledEventHandler(bool show);
    [Signal] public delegate void DebugMenuToggledEventHandler(bool show);


    [Signal] public delegate void PlayerJoinedEventHandler();
    [Signal] public delegate void PlayerLeftEventHandler();
    [Signal] public delegate void GameSessionStartedEventHandler();

    /// <summary>
    /// A save-game file was written. <c>deferred</c> is true when the player asked for it at a moment the
    /// game could not honour and GameFlow flushed it at the next boundary, which is worth saying
    /// differently in the confirmation.
    /// </summary>
    [Signal] public delegate void GameSavedEventHandler(string displayName, bool deferred);

    [Signal] public delegate void FactionsAssignedEventHandler();

    [Signal] public delegate void CountryClickedEventHandler(int countryClicked);
    [Signal] public delegate void UnitClickedEventHandler(int unitClicked);

    // Emitted when the player presses the Skip button during a card-step board selection.
    [Signal] public delegate void SelectionSkippedEventHandler();

    /// <summary>Emitted when the player presses one of the scoped skip buttons in a reaction
    /// window. The argument is a <see cref="ReactionSkipScope"/>.</summary>
    [Signal] public delegate void ReactionSkipScopedEventHandler(int scope);

    /// <summary>Emitted when a faction's <see cref="ReactionSkipPreference"/> setting changes,
    /// whether from the row toggle or from a scope pressed on a live prompt. The argument is a
    /// <see cref="Faction"/>.</summary>
    [Signal] public delegate void ReactionSkipPreferenceChangedEventHandler(int faction);

    [Signal] public delegate void GameChangeEventOccurredEventHandler();

    [Signal] public delegate void CardSelectedEventHandler(int cardId);

    /// <summary>A card prompt opened on this peer. The argument is the prompted <see cref="Faction"/>.</summary>
    [Signal] public delegate void CardPromptOpenedEventHandler(int faction);
    /// <summary>The open card prompt resolved (a card was chosen, passed or the host abandoned it).</summary>
    [Signal] public delegate void CardPromptClosedEventHandler();

    /// <summary>What the local client is busy with changed — see <see cref="FactionFocus"/>. The
    /// argument is the focused <see cref="Faction"/>, or <see cref="Faction.NONE"/> when idle.</summary>
    [Signal] public delegate void FactionFocusChangedEventHandler(int faction);

    /// <summary>What the recall button can bring back changed — a prompt was parked, recalled or
    /// resolved. See <see cref="RecallablePrompts"/>.</summary>
    [Signal] public delegate void RecallablePromptChangedEventHandler();

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

    /// <summary>
    /// A win condition was met and the serialized <see cref="GameResult"/> is final. Emitted on every
    /// peer at the top of MultiplayerSession.BeginEndGame, i.e. BEFORE the queue drain and the switch
    /// to the victory screen.
    ///
    /// Early on purpose: it is the only game-over notification a process with no UI can observe, and
    /// an automated run wants to report the result and exit rather than load VictoryScreen.tscn.
    /// Carries the JSON rather than the object because GameResult is a plain class, not a Variant.
    /// </summary>
    [Signal] public delegate void GameEndedEventHandler(string resultJson);

    [Signal] public delegate void FactionScoredPointsEventHandler(Faction faction, int points);

    [Signal] public delegate void UnitDeployedEventHandler(int unitId, int countryId);
    [Signal] public delegate void UnitRemovedEventHandler(int unitId, int countryId);

    [Signal] public delegate void GameStateRecalculatedEventHandler();

    [Signal] public delegate void WorldPresentationViewChangedEventHandler(WorldPresentationMode mode);

    /// <summary>The player pressed continue on a commander message, so whatever is holding that
    /// message on screen should move on. See <see cref="CommanderMessage"/>.</summary>
    [Signal] public delegate void CommanderMessageContinuedEventHandler();
}
