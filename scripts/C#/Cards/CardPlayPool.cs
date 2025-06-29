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

    public static void ClearPool()
    {
        CardPool.Clear();
        ChangeEventsPool.Clear();
        LastChangeEvent = null;
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

        if (changeEvent.IsTrigger)
        {
            if (changeEvent is not PlayCardChangeEvent && changeEvent is not ActivateReactionChangeEvent)
            {
                DebugUtilities.PrintPeer("RequestAfterChangeEventReaction");
                DoNextActions();
            }
            else
            {
                DebugUtilities.PrintPeer("New Card played");
            }
        }
    }

    public static async void DoNextActions()
    {
        bool reactionPlayed = await RequestAfterChangeEventReaction();
        if (reactionPlayed == false)
        {
            DebugUtilities.PrintPeer("No Reaction Was played");
            await Task.Delay(500);
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
            bool reactionPlayed = await RequestActivationOption(faction);
            if (reactionPlayed) return true;
        }
        return false;
    }
    public async static Task<bool> RequestActivationOption(Faction faction)
    {        
        List<CardActivationOption> activationOptions = GetNextActions(faction);
        if (activationOptions != null && activationOptions.Count > 0)
        {
            CardActivationOption cardActivationOption = await GameSession.RequestPlay(faction);
            DebugUtilities.PrintPeer("cardActivationOption");
            if (cardActivationOption != null)
            {
                CardLogic cardLogic = cardActivationOption.CardState.CardLogic;

                if (!cardLogic.IsPlayed)
                {
                    PlayCardChangeEvent playCardChangeEvent = new PlayCardChangeEvent(Faction.GERMANY, cardActivationOption.CardId);
                    playCardChangeEvent.IsTrigger = true;
                    DoChangeEvent(playCardChangeEvent);
                }
                else if (cardLogic.IsPlayed && cardLogic.IsReaction && !cardLogic.IsActivatedThisTurn)
                {
                    ActivateReactionChangeEvent activateReactionChangeEvent = new ActivateReactionChangeEvent(Faction.GERMANY, cardActivationOption.CardId, null);
                    activateReactionChangeEvent.IsTrigger = true;
                    DoChangeEvent(activateReactionChangeEvent);
                }
                else
                {
                    CardStep cardStep = CardStep.ForId(cardActivationOption.StepId);
                    PlayerActionLabel.ShowText(cardStep.CardLogic.ActivateActionGuidance(), -1, cardStep.CardLogic.Faction);
                    cardStep.Execute();
                }
                return true;
            }
        }
        else
        {
            DebugUtilities.PrintPeer($"{faction} does have activation options");
        }
        return false;
        
    }

    public static List<CardActivationOption> GetNextActions(Faction faction)
    {
        List<CardActivationOption> allActivationOptions = new List<CardActivationOption>();
        if (CardPool.Count == 0 && GameSession.Instance.GameFlow.TurnStep == TurnStep.PLAY_CARD)
        {
            allActivationOptions.AddRange(PlayableCards(faction)); //Should be empty on start play
        }
        allActivationOptions.AddRange(ActivatableReactions(faction, false));     
        return allActivationOptions;
    }
    public static List<CardActivationOption> PlayableCards(Faction faction)
    {
        List<CardActivationOption> activationOptions = new List<CardActivationOption>();
        foreach (CardState cardState in DeckState.ForFaction(faction).HandCardStates)
        {
            if (cardState.CardLogic == null) continue;
            foreach (CardStep cardStep in cardState.CardLogic.PlayCardSteps)
            {
                if (cardStep.PrerequisiteStepFinished)
                {
                    activationOptions.Add(new CardActivationOption(cardState.Id, cardStep.Id, cardState.CardData.Label, cardState.CardLogic != null ? cardState.CardLogic.CanPlayCard() : false));
                }

            }            
        }
        return activationOptions;
    }
    public static List<CardActivationOption> PartialCards(Faction faction)
    {
        List<CardActivationOption> activationOptions = new List<CardActivationOption>();
        foreach (CardState cardState in CardPool.Values.ToList().Where(cardState => cardState.Faction == faction))
        {
            foreach (CardStep cardStep in cardState.CardLogic.ExecutablePlaySteps)
            {
                activationOptions.Add(new CardActivationOption(cardState.Id, cardStep.Id, cardState.CardData.Label, true));
            }
            foreach (CardStep cardStep in cardState.CardLogic.ExecutableReactSteps)
            {
                activationOptions.Add(new CardActivationOption(cardState.Id, cardStep.Id, cardState.CardData.Label, true));
            }
        }
        return activationOptions;
    }
    private static List<CardActivationOption> ActivatableReactions(Faction faction, bool isBeforeReaction)
    {
        DeckState deckState = DeckState.ForFaction(faction);
        List<CardActivationOption> allActivationOptions = new List<CardActivationOption>();
        List<CardActivationOption> statusActivationOptions = ActivatableCards(deckState.StatusCardStates);
        List<CardActivationOption> responseActivationOptions = ActivatableCards(DeckState.ForFaction(faction).ResponseCardStates);
        allActivationOptions.AddRange(statusActivationOptions);
        allActivationOptions.AddRange(responseActivationOptions);
        return allActivationOptions;
    }
    private static List<CardActivationOption> ActivatableCards(List<CardState> cardStates)
    {
        var options = new List<CardActivationOption>();
        foreach (CardState cardState in cardStates)
        {
            bool canActivate = cardState.CardLogic.CanBeActivated();
            if (canActivate)
            {
                CardStep nextStep = cardState.CardLogic.ReactCardSteps[0];                
                CardActivationOption option = new CardActivationOption(cardState.Id, nextStep.Id, cardState.CardData.Label, cardState.CardLogic != null ? cardState.CardLogic.CanBeActivated() : false);
                options.Add(option);                
            }
            else if (cardState.CardLogic.IsActivatedThisTurn && cardState.CardLogic.ExecutableReactSteps.Count > 0)
            {
                CardStep nextStep = cardState.CardLogic.ExecutableReactSteps[0];
                CardLogic cardLogic = nextStep.CardLogic;
                CardActivationOption option = new CardActivationOption(cardState.Id, nextStep.Id, cardLogic.CardState.CardData.Label, true);
                options.Add(option);
            }
        }
        return options;
    }    
}
