using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;

public partial class CardPlayPool : GodotObject
{
    public static List<CardState> CardPool = new();
    public static Dictionary<int, CardState> CardPoolMap
    {
        get
        {
            Dictionary<int, CardState> response = new();
            foreach (CardState cardState in CardPool)
            {
                response.Add(cardState.Id, cardState);
            }
            return response;
        }
    }

    public static List<ChangeEvent> ChangeEventsPool = new();
    public static Dictionary<int, ChangeEvent> ChangeEventsPoolMap
    {
        get
        {
            Dictionary<int, ChangeEvent> response = new();
            foreach (ChangeEvent changeEvent in ChangeEventsPool)
            {
                response.Add(changeEvent.Id, changeEvent);
            }
            return response;
        }
    }

    public static ChangeEvent LastChangeEvent;
    public static Faction LastChangeEventByFaction
    {
        get { return LastChangeEvent.TriggeringFaction; }
    }
    public static FactionTeam LastChangeEventByTeam
    {
        get { return StaticGameData.OpponentFactionTeamForFaction(LastChangeEventByFaction); }
    }

    public static ChangeEvent LastNoneNewCardChangeEvent
    {
        get
        {
            for (int i = ChangeEventsPool.Count - 1; i >= 0; i--)
            {
                ChangeEvent changeEvent = ChangeEventsPool[i];
                if (changeEvent is not PlayCardChangeEvent && changeEvent is not ActivateReactionChangeEvent)
                    return changeEvent;
            }
            return null;
        }
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
    public static void AddCard(CardState cardState)
    {
        CardPool[cardState.Id] = cardState;
    }
    public static void AddChangeEvent(ChangeEvent changeEvent)
    {
        ChangeEventsPool[changeEvent.Id] = changeEvent;
    }

    public static List<T> GetChangeEvents<T>() where T : ChangeEvent
    {
        List<ChangeEvent> changeEvents = CardPlayPool.ChangeEventsPool.Where(changeEvent => changeEvent is T).ToList();
        return changeEvents.Map(changeEvent => changeEvent as T);
    }
    public static async Task DoChangeEvent(ChangeEvent changeEvent)
    {
        int sourceCardId = changeEvent.SourceCardId;
        if (sourceCardId > -1 && !CardPoolMap.ContainsKey(sourceCardId))
        {
            CardPool.Add(changeEvent.SourceCardState);
        }
        ChangeEventsPool.Add(changeEvent);
        LastChangeEvent = changeEvent;

        if (changeEvent.IsTrigger)
        {
            foreach (Faction faction in RequestOrder)
            {
                CardActivationOption cardActivationOption = await GameSession.RequestBlock(faction);
                if (cardActivationOption != null)
                {
                    DebugUtilities.PrintPeer($"AWAITING BLOCK FROM {FactionState.ForEnum(faction).FactionLabel} for {changeEvent?.GetType().Name}");
                    await DoActivationOption(cardActivationOption);
                    DebugUtilities.PrintPeer($"BLOCKED AWAWITED {changeEvent?.GetType().Name}");
                }
            }
        }

        if (changeEvent.IsBlocked == false)
        {
            LastChangeEvent = changeEvent;
            changeEvent.ChangeEventApplied += (int changeEventId) =>
            {
                ChangeEvent ce = ChangeEvent.ForId(changeEventId);                
                DebugUtilities.PrintPeer($"CardPlayPool.Applied {ce?.GetType().Name}");
            };
            await changeEvent.ApplyChange();
        }

        if (changeEvent.IsTrigger)
        {
            if (changeEvent is PlayCardChangeEvent || changeEvent is ActivateReactionChangeEvent)
            {
                DebugUtilities.PrintPeer($"New Card was played, do not check for new actions");
            }
            else
            {
                DebugUtilities.PrintPeer($"RequestAfterChangeEventReaction  {changeEvent?.GetType().Name}");
                DoNextActions();
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

    public async static Task<List<CardActivationOption>> BlockChangeEvents(Faction faction)
    {
        if (LastChangeEvent?.TriggeringFaction == faction)
        {
            return []; //Cant block yourself
        }
        List<CardActivationOption> cardActivationOptions = CardPlayPool.ActivatableReactions(faction, true);
        cardActivationOptions = cardActivationOptions.Where(cao => cao.CardState.CardLogic.IsBlockReaction).ToList();
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
        return cardActivationOptions;
    }

    public async static Task DoActivationOption(CardActivationOption cardActivationOption){
        if (cardActivationOption != null)
        {
            CardLogic cardLogic = cardActivationOption.CardState.CardLogic;

            if (!cardLogic.IsPlayed)
            {
                PlayCardChangeEvent playCardChangeEvent = new PlayCardChangeEvent(cardActivationOption.CardState.Faction, cardActivationOption.CardId);
                playCardChangeEvent.IsTrigger = true;
                await DoChangeEvent(playCardChangeEvent);
            }
            else if (cardLogic.IsPlayed && cardLogic.IsReaction && !cardLogic.IsActivatedThisTurn)
            {
                ActivateReactionChangeEvent activateReactionChangeEvent = new ActivateReactionChangeEvent(cardActivationOption.CardState.Faction, cardActivationOption.CardId, null);
                activateReactionChangeEvent.IsTrigger = true;
                DebugUtilities.PrintPeer("ActivateReactionChangeEvent1");
                await DoChangeEvent(activateReactionChangeEvent);
                DebugUtilities.PrintPeer("ActivateReactionChangeEvent2");
            }
            else
            {
                CardStep cardStep = CardStep.ForId(cardActivationOption.StepId);
                PlayerActionLabel.ShowText(cardStep.CardLogic.ActivateActionGuidance(), -1, cardStep.CardLogic.Faction);
                DebugUtilities.PrintPeer("EXECUTING");
                await cardStep.Execute();
                DebugUtilities.PrintPeer("FINISHED EXECUTING");
            }
        }
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
            if (cardActivationOption == null)
            {
                return false;
            }
            DebugUtilities.PrintPeer("cardActivationOption");            
            _ = DoActivationOption(cardActivationOption);
            return true;
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
