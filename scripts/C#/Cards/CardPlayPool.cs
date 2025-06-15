using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;

public partial class CardPlayPool : GodotObject
{       

    public static Dictionary<int, CardState> CardPool = new Dictionary<int, CardState>();
    public static Dictionary<int, ChangeEvent> ChangeEventsPool = new Dictionary<int, ChangeEvent>();

    public static ChangeEvent LastChangeEvent;
    public static Faction LastChangeEventByFaction;
    public static FactionTeam LastChangeEventByTeam
    {
        get { return StaticGameData.OpponentFactionTeamForFaction(LastChangeEventByFaction); }
    }
    public static List<Faction> RequestOrder
    {
        get
        {
            return LastChangeEventByTeam == FactionTeam.AXIS
                ? new List<Faction> { Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES, Faction.GERMANY, Faction.JAPAN, Faction.ITALY } 
                : new List<Faction> { Faction.GERMANY, Faction.JAPAN, Faction.ITALY, Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES };
        }
    }

    public static void ClearPool(){
        CardPool.Clear();
        ChangeEventsPool.Clear();
    }
    public static void AddCard(CardState cardState){
        CardPool[cardState.Id] = cardState;
    }
    public static void AddChangeEvent(ChangeEvent changeEvent){
        ChangeEventsPool[changeEvent.Id] = changeEvent;        
    }

    public static List<T> GetChangeEvents<T>() where T : ChangeEvent
    {
        List<ChangeEvent> changeEvents = CardPlayPool.ChangeEventsPool.Values.ToList().Where(changeEvent => changeEvent is T).ToList();
        return changeEvents.Map(changeEvent => changeEvent as T);
    }


    public static async Task DoChangeEvent(ChangeEvent changeEvent)
    {
        int sourceCardId = changeEvent.SourceCardId;
        if (sourceCardId != null && sourceCardId > -1 && !CardPool.ContainsKey(sourceCardId))
        {
            CardPool.Add(changeEvent.SourceCardId, changeEvent.SourceCardState);
        }
        ChangeEventsPool.Add(changeEvent.Id, changeEvent);
        LastChangeEvent = changeEvent;

        await RequestBlockChangeEvent();

        if (changeEvent.IsBlocked == false)
        {
            LastChangeEvent = changeEvent;
            LastChangeEventByFaction = changeEvent.TriggeringFaction;
            changeEvent.ChangeEventApplied += (int changeEventId) =>
            {
                DebugUtilities.PrintPeer("CardPlayPool.Applied");
            };
            await changeEvent.ApplyChange();
        }
        DebugUtilities.PrintPeer("RequestAfterChangeEventReaction");
        if (changeEvent is not PlayCardChangeEvent)
        {
            DoNextActions();
        }
    }

    public static async void DoNextActions()
    {
        bool reactionPlayed = await RequestAfterChangeEventReaction();
        if (reactionPlayed == false)
        {
            ClearPool();
            EventBus.Emit(EventBus.SignalName.CardPlayPoolFinished);            
        }
    }

    private async static Task<bool> RequestBlockChangeEvent()
    {
        if (LastChangeEvent.IsTrigger)
        {
            foreach (Faction faction in GameSession.FactionStates.Keys)
            {
                await RequestBlockChangeEvent(LastChangeEvent, faction);
            }
        }
        return true;
    }
    
    private async static Task<ChangeEvent> RequestBlockChangeEvent(ChangeEvent changeEvent, Faction faction)
    {
        List<CardActivationOption> cardActivationOptions = CardPlayPool.ActivatableReactions(faction, true);
        if (cardActivationOptions != null && cardActivationOptions.Count > 0)
        {
            DebugUtilities.PrintPeer($"{faction} does have reaction options {cardActivationOptions.Count}");
            await Task.Delay(500);
        }
        else
        {
            DebugUtilities.PrintPeer($"{faction} does not block change events !!BEFORE!!");
            await Task.Delay(10);
        }
        return null;
    }

    private async static Task<bool> RequestAfterChangeEventReaction()
    {
        foreach (Faction faction in RequestOrder)
        {
            bool reactionPlayed = await RequestAfterChangeEventReaction(faction);
            if (reactionPlayed) return true;
        }
        return false;
    }
    private async static Task<bool> RequestAfterChangeEventReaction(Faction faction)
    {        
        List<CardActivationOption> activationOptions = GetNextActions(faction);
        if (activationOptions != null && activationOptions.Count > 0)
        {
            CardState cardState = await GameSession.RequestCardPlay(faction);
            if (cardState != null)
            {
                ActivateReactionChangeEvent playCardChangeEvent = new ActivateReactionChangeEvent(Faction.GERMANY, cardState.Id, null);
                playCardChangeEvent.IsTrigger = true;
                DoChangeEvent(playCardChangeEvent);
                return true;
            }
        }
        return false;
        
    }

    public static List<CardActivationOption> GetNextActions(Faction faction)
    {
        List<CardActivationOption> allActivationOptions = new List<CardActivationOption>();
        if (CardPool.Count == 0)
        {
            allActivationOptions.AddRange(PlayableCards(faction));
        }
        allActivationOptions.AddRange(ActivatableReactions(faction, false)); //Should be empty on start play
        return allActivationOptions;
    }
    public static List<CardActivationOption> PlayableCards(Faction faction)
    {
        List<CardActivationOption> activationOptions = new List<CardActivationOption>();
        foreach (CardState cardState in DeckState.ForFaction(faction).HandCardStates)
        {
            activationOptions.Add(new CardActivationOption(cardState.Id, cardState.CardData.Label, cardState.CardLogic != null ? cardState.CardLogic.CanPlayCard() : false));
        }
        return activationOptions;
    }
    private static List<CardActivationOption> ActivatableReactions(Faction faction, bool isBeforeReaction)
    {
        List<CardActivationOption> allActivationOptions = new List<CardActivationOption>();
        List<CardActivationOption> statusActivationOptions = ActivatableStatuses(faction, isBeforeReaction);
        List<CardActivationOption> responseActivationOptions = ActivatableResponse(faction, isBeforeReaction);

        allActivationOptions.AddRange(statusActivationOptions);
        allActivationOptions.AddRange(responseActivationOptions);
        return allActivationOptions;
    }
    private static List<CardActivationOption> ActivatableStatuses(Faction faction, bool isBeforeReaction = false) {
        DeckState deckState = DeckState.ForFaction(faction);
        List<CardActivationOption> options = new List<CardActivationOption>();
        foreach(ChangeEvent changeEvent in ChangeEventsPool.Values)
        {
            options.AddRange(ActivatableCardsForChangeEvent(deckState.StatusCardStates, changeEvent, isBeforeReaction));
        }
        return options;
    }
    private static List<CardActivationOption> ActivatableResponse(Faction faction, bool isBeforeReaction = false) {
        DeckState deckState = DeckState.ForFaction(faction);
        List<CardActivationOption> options = new List<CardActivationOption>();
        foreach(ChangeEvent changeEvent in ChangeEventsPool.Values)
        {
            options.AddRange(ActivatableCardsForChangeEvent(deckState.ResponseCardStates, changeEvent, isBeforeReaction));
        }
        return options;
    }
    private static List<CardActivationOption> ActivatableCardsForChangeEvent(List<CardState> cardStates, ChangeEvent changeEvent, bool isBeforeReaction = false)
    {
        var options = new List<CardActivationOption>();
        foreach (CardState cardState in cardStates)
        {
            bool canActivate = isBeforeReaction
                ? cardState.CardLogic.CanReactTo(changeEvent)
                : cardState.CardLogic.CanReactTo(changeEvent);

            if (canActivate)
            {
                CardActivationOption option = new CardActivationOption(cardState.Id, cardState.CardData.Label, cardState.CardLogic != null ? cardState.CardLogic.CanPlayCard() : false);
                options.Add(option);
            }
        }
        return options;
    }    
}
