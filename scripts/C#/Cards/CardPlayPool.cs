using Godot;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;

public partial class CardPlayPool : GodotObject
{
    public static List<CardState> CardPool = new();
    public static List<ChangeEvent> ChangeEventsPool = new();
    public static ChangeEvent LastChangeEvent;
    
    private static int reactionDepth = 0; // Track nesting depth of reactions

    public static Dictionary<int, CardState> CardPoolMap => CardPool.ToDictionary(c => c.Id, c => c);    
    public static Dictionary<int, ChangeEvent> ChangeEventsPoolMap => ChangeEventsPool.ToDictionary(c => c.Id, c => c);
    
    public static Faction LastChangeEventByFaction => LastChangeEvent != null ? LastChangeEvent.TriggeringFaction : Faction.GERMANY;
    public static FactionTeam LastChangeEventByTeam => StaticGameData.OpponentFactionTeamForFaction(LastChangeEventByFaction);
    public static ChangeEvent LastNoneNewCardChangeEvent => ChangeEventsPool.LastOrDefault(c => c is not PlayCardChangeEvent && c is not ActivateReactionChangeEvent);

    public static List<Faction> RequestOrder => LastChangeEventByTeam == FactionTeam.AXIS ? AlliesFirstOrder : AxisFirstOrder;
    public static List<Faction> AxisFirstOrder => new List<Faction> { Faction.GERMANY, Faction.JAPAN, Faction.ITALY, Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES };
    public static List<Faction> AlliesFirstOrder => new List<Faction> { Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES, Faction.GERMANY, Faction.JAPAN, Faction.ITALY };

    public static void ClearPool()
    {
        CardPool.Clear();
        ChangeEventsPool.Clear();
        LastChangeEvent = null;
        reactionDepth = 0;
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

    /// <summary>
    /// Executes only the first CardStep of a card (which moves it to status/response area) without full activation.
    /// Automatically clears the pool after execution.
    /// </summary>
    public static async Task AddCardToPlayAreaWithoutActivating(int cardNumber)
    {
        CardState cardState = CardState.ForNumber(cardNumber);
        if (cardState == null)
        {
            DebugUtilities.PrintPeerError($"Card number {cardNumber} not found");
            return;
        }
        await AddCardToPlayAreaWithoutActivating(cardState);
    }

    public static async Task AddCardToPlayAreaWithoutActivating(string cardName)
    {
        CardState cardState = CardState.ForName(cardName);
        if (cardState == null)
        {
            DebugUtilities.PrintPeerError($"Card not found: {cardName}");
            return;
        }
        
        await AddCardToPlayAreaWithoutActivating(cardState);
    }

    private static async Task AddCardToPlayAreaWithoutActivating(CardState cardState)
    {
        List<CardStep> playSteps = cardState.CardLogic.PlayCardSteps;
        if (playSteps.Count == 0)
        {
            DebugUtilities.PrintPeerError($"Card {cardState.CardData.UniqueName} has no play steps");
            ClearPool();
            return;
        }

        // Mirror DoActivationOption but with IsTrigger=false so no reactions are checked
        var playCardChangeEvent = new PlayCardChangeEvent(cardState.Faction, cardState.Id);
        playCardChangeEvent.IsTrigger = false;
        await DoChangeEvent(playCardChangeEvent);

        // Execute the first play step (moves card to status/response area)
        ChangeEvent stepResult = await cardState.CardLogic.PlayCard(playSteps[0].Id);

        // Apply the step result without triggering reactions
        if (stepResult != null)
        {
            stepResult.IsTrigger = false;
            await DoChangeEvent(stepResult);
        }

        DebugUtilities.PrintPeer($"Added {cardState.CardData.UniqueName} to play area without activation");
        ClearPool();
    }


    public async static Task DoActivationOption(CardActivationOption cardActivationOption) {
        if (cardActivationOption != null)
        {
            CardLogic cardLogic = cardActivationOption.CardState.CardLogic;
            CardStep cardStep = CardStep.ForId(cardActivationOption.StepId);
            ChangeEvent cardIntroductionChangeEvent = null;
            
            bool isInitialPlay = reactionDepth == 0;
            reactionDepth++; // Increment depth when processing a card

            // Step 1: Create and process introduction event (PlayCard or ActivateReaction)
            if (!cardLogic.IsPlayed)
            {
                cardIntroductionChangeEvent = new PlayCardChangeEvent(cardActivationOption.CardState.Faction, cardActivationOption.CardId);
                cardIntroductionChangeEvent.IsTrigger = true;
                await DoChangeEvent(cardIntroductionChangeEvent);
            }
            else if (cardLogic.IsPlayed && cardLogic.IsReaction && !cardLogic.IsActivatedThisTurn)
            {
                cardIntroductionChangeEvent = new ActivateReactionChangeEvent(cardActivationOption.CardState.Faction, cardActivationOption.CardId, null);
                cardIntroductionChangeEvent.IsTrigger = true;
                await DoChangeEvent(cardIntroductionChangeEvent);
            }
            
            // Step 2: If introduction wasn't blocked, execute the card step
            if (cardIntroductionChangeEvent == null || (cardIntroductionChangeEvent != null && !cardIntroductionChangeEvent.IsBlocked))
            {
                ChangeEvent stepResultChangeEvent = null;
                if (cardIntroductionChangeEvent is PlayCardChangeEvent)
                {
                    stepResultChangeEvent = await cardActivationOption.CardState.CardLogic.PlayCard(cardStep.Id);                    
                }
                else if (cardIntroductionChangeEvent is ActivateReactionChangeEvent && GameSession.IsStarted)
                {
                    stepResultChangeEvent = await cardActivationOption.CardState.CardLogic.React(cardStep.Id);                    
                }
                else if (cardIntroductionChangeEvent == null && cardLogic.IsActivatedThisTurn && GameSession.IsStarted)
                {
                    // Continuation step for an already-activated reaction card (step 2+)
                    stepResultChangeEvent = await cardActivationOption.CardState.CardLogic.React(cardStep.Id);
                }
                
                // Step 3: Process the step result (this will handle blocks, apply, and after-reactions)
                if (stepResultChangeEvent != null)
                {
                    await DoChangeEvent(stepResultChangeEvent);
                }                
            }
            
            reactionDepth--; // Decrement depth when done
            
            // If this was the initial card play (depth now 0), check if everything is complete
            if (reactionDepth == 0 && isInitialPlay)
            {
                DebugUtilities.PrintPeer("Initial card play complete, checking for any remaining actions...");
                // Check if there are any more executable actions
                bool hasAnyActions = false;
                foreach (Faction faction in RequestOrder)
                {
                    List<CardActivationOption> options = GetAfterReactionOptions(faction);
                    if (options != null && options.Count > 0)
                    {
                        hasAnyActions = true;
                        break;
                    }
                }
                
                if (!hasAnyActions)
                {
                    DebugUtilities.PrintPeer("No more actions available - Play step complete");
                    ClearPool();
                    EventBus.Emit(EventBus.SignalName.CardPlayPoolFinished);
                }
            }
        }
    }

    public static async Task DoChangeEvent(ChangeEvent changeEvent)
    {
        // Step 1: Add change event and source card to the pool
        int sourceCardId = changeEvent.SourceCardId;
        if (sourceCardId > -1 && !CardPoolMap.ContainsKey(sourceCardId))
        {
            CardPool.Add(changeEvent.SourceCardState);
        }
        ChangeEventsPool.Add(changeEvent);
        LastChangeEvent = changeEvent;

        // Step 2: Request BLOCK reactions (before applying change)
        if (changeEvent.IsTrigger)
        {
            await RequestBlockReactions(changeEvent);
        }

        // Step 3: Apply the change if not blocked
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

        // Step 4: Request AFTER reactions (after applying change, for non-introduction events)
        // Introduction events (PlayCard/ActivateReaction) don't trigger after-reactions
        // because the actual card step will create its own change event
        if (changeEvent is not PlayCardChangeEvent && changeEvent is not ActivateReactionChangeEvent)
        {
            if (changeEvent.IsTrigger && !changeEvent.IsBlocked)
            {
                await RequestAfterReactions();
            }
        }
    }
    
    /// <summary>
    /// Request block reactions from all factions in order
    /// Block reactions can prevent a change from being applied
    /// </summary>
    private static async Task RequestBlockReactions(ChangeEvent changeEvent)
    {
        foreach (Faction faction in RequestOrder)
        {
            // Skip if this faction triggered the event
            if (faction == changeEvent.TriggeringFaction)
            {
                continue;  // Can react to your own actions with after-reactions, but not block them
            }
            
            CardActivationOption cardActivationOption = await GameSession.RequestBlock(faction);
            if (cardActivationOption != null)
            {
                DebugUtilities.PrintPeer($"BLOCK REACTION from {FactionState.ForEnum(faction).FactionLabel} for {changeEvent?.GetType().Name}");
                // Recursively process this reaction (which may itself trigger more reactions)
                await DoActivationOption(cardActivationOption);
                DebugUtilities.PrintPeer($"BLOCK REACTION FINISHED");
            }
        }
    }
    
    /// <summary>
    /// Request after reactions from all factions in order
    /// This creates a recursive chain where responses can be responded to
    /// </summary>
    private static async Task RequestAfterReactions()
    {
        DebugUtilities.PrintPeer($"RequestAfterReactions called with {ChangeEventsPool.Count} events in pool");
        foreach (var ev in ChangeEventsPool)
        {
            DebugUtilities.PrintPeer($"  Event: {ev.GetType().Name}");
        }
        
        bool anyReactionPlayed = true;
        
        // Keep requesting reactions until no one plays anything
        while (anyReactionPlayed)
        {
            anyReactionPlayed = false;
            
            foreach (Faction faction in RequestOrder)
            {
                List<CardActivationOption> activationOptions = GetAfterReactionOptions(faction);
                
                if (activationOptions != null && activationOptions.Count > 0)
                {
                    CardActivationOption cardActivationOption = await GameSession.RequestPlay(faction);
                    if (cardActivationOption != null)
                    {
                        DebugUtilities.PrintPeer($"AFTER REACTION from {FactionState.ForEnum(faction).FactionLabel}");
                        // Recursively process this reaction (which may itself trigger blocks and after-reactions)
                        await DoActivationOption(cardActivationOption);
                        anyReactionPlayed = true;
                        // Break to restart the faction order with updated RequestOrder
                        break;
                    }
                }
            }
        }
        
        // After all reactions are complete, check if there are next steps to execute
        await ContinueWithNextSteps();
    }
    
    /// <summary>
    /// Get after-reaction options for a faction (reactions that are not block reactions)
    /// </summary>
    private static List<CardActivationOption> GetAfterReactionOptions(Faction faction)
    {
        DebugUtilities.PrintPeer($"GetAfterReactionOptions for {faction}: Checking with {ChangeEventsPool.Count} events in pool");
        List<CardActivationOption> allOptions = GetNextActions(faction);
        // Filter out block reactions - we only want after-reactions here
        List<CardActivationOption> afterReactions = allOptions.Where(option => !option.CardState.CardLogic.IsBlockReaction).ToList();
        DebugUtilities.PrintPeer($"  Found {allOptions.Count} total options, {afterReactions.Count} after-reactions");
        return afterReactions;
    }
    
    /// <summary>
    /// Check if any cards in the pool have executable next steps and execute them
    /// This allows multi-step cards to continue after reactions are complete
    /// Returns true if any steps were executed
    /// </summary>
    private static async Task<bool> ContinueWithNextSteps()
    {
        bool anyStepsExecuted = false;
        bool hasMoreSteps = true;
        
        while (hasMoreSteps)
        {
            hasMoreSteps = false;
            
            // Check all cards in the pool for executable next steps
            foreach (CardState cardState in CardPool)
            {
                if (cardState.CardLogic == null) continue;
                
                List<CardStep> nextSteps = new List<CardStep>();
                
                // Check for next play steps
                if (cardState.CardLogic.IsPlayed && !cardState.CardLogic.IsReaction)
                {
                    nextSteps = cardState.CardLogic.ExecutablePlaySteps;
                }
                // Check for next react steps
                else if (cardState.CardLogic.IsActivatedThisTurn)
                {
                    nextSteps = cardState.CardLogic.ExecutableReactSteps;
                }
                
                if (nextSteps.Count > 0)
                {
                    CardStep nextStep = nextSteps[0];
                    DebugUtilities.PrintPeer($"Continuing with next step for {cardState.CardName}");
                    
                    // Execute the next step
                    ChangeEvent stepResultChangeEvent = null;
                    if (cardState.CardLogic.IsPlayed && !cardState.CardLogic.IsReaction)
                    {
                        stepResultChangeEvent = await cardState.CardLogic.PlayCard(nextStep.Id);
                    }
                    else if (cardState.CardLogic.IsActivatedThisTurn && GameSession.IsStarted)
                    {
                        stepResultChangeEvent = await cardState.CardLogic.React(nextStep.Id);
                    }
                    
                    // Process the step result (which handles blocks, apply, and after-reactions)
                    if (stepResultChangeEvent != null)
                    {
                        await DoChangeEvent(stepResultChangeEvent);
                    }
                    
                    anyStepsExecuted = true;
                    hasMoreSteps = true;
                    break; // Restart the loop after processing a step
                }
            }
        }
        
        return anyStepsExecuted;
    }
    
    public static List<CardActivationOption> GetNextActions(Faction faction)
    {
        List<CardActivationOption> allActivationOptions = new List<CardActivationOption>();
        
        // If no cards in pool yet and we're in play step, allow playing cards from hand
        if (CardPool.Count == 0 && GameSession.Instance.GameFlow.TurnStep == TurnStep.PLAY_CARD)
        {
            allActivationOptions.AddRange(PlayableCards(faction));
        }
        
        // Add activatable reactions (both new activations and continued steps)
        allActivationOptions.AddRange(ActivatableReactions(faction));
        
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
                    activationOptions.Add(new CardActivationOption(
                        cardState.Id, 
                        cardStep.Id, 
                        cardState.CardData.Label, 
                        cardState.CardLogic.CanPlayCard()
                    ));
                }
            }
        }
        return activationOptions;
    }
    
    /// <summary>
    /// Get activatable reactions for a faction
    /// This includes both new reactions that can be activated and next steps of already-activated reactions
    /// </summary>
    private static List<CardActivationOption> ActivatableReactions(Faction faction)
    {
        DeckState deckState = DeckState.ForFaction(faction);
        List<CardActivationOption> allActivationOptions = new List<CardActivationOption>();
        
        DebugUtilities.PrintPeer($"ActivatableReactions for {faction}: Checking {deckState.StatusCardStates.Count} status cards and {deckState.ResponseCardStates.Count} response cards");
        
        // Get activatable status and response cards
        List<CardActivationOption> statusActivationOptions = ActivatableCards(deckState.StatusCardStates);
        List<CardActivationOption> responseActivationOptions = ActivatableCards(deckState.ResponseCardStates);
        
        DebugUtilities.PrintPeer($"  Found {statusActivationOptions.Count} activatable status cards and {responseActivationOptions.Count} activatable response cards");
        
        allActivationOptions.AddRange(statusActivationOptions);
        allActivationOptions.AddRange(responseActivationOptions);
        
        return allActivationOptions;
    }
    
    /// <summary>
    /// Get activation options for a list of card states
    /// Handles both initial activation and continued steps
    /// </summary>
    private static List<CardActivationOption> ActivatableCards(List<CardState> cardStates)
    {
        var options = new List<CardActivationOption>();
        foreach (CardState cardState in cardStates)
        {
            DebugUtilities.PrintPeer($"Checking card: {cardState.CardName}, CardState.Id={cardState.Id}, IsPlayed={cardState.CardLogic.IsPlayed}, CardLogic instance={cardState.CardLogic.GetInstanceId()}");
            bool canActivate = cardState.CardLogic.CanBeActivated();
            if (canActivate)
            {
                if (cardState.CardLogic.ReactCardSteps.Count == 0)
                {
                    throw new NotImplementedException($"{cardState.CardData.UniqueName} does not have REACT logic implemented: {cardState.CardLogic.GetClass()}");
                    
                }
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
    
    /// <summary>
    /// Get block reaction options for a faction (called by GameSession.RequestBlock)
    /// Only returns reactions that have IsBlockRequest trigger
    /// </summary>
    public async static Task<List<CardActivationOption>> BlockChangeEvents(Faction faction)
    {
        // Can't block your own actions
        if (LastChangeEvent?.TriggeringFaction == faction)
        {
            return new List<CardActivationOption>();
        }
        
        // Get all activatable reactions for this faction
        List<CardActivationOption> cardActivationOptions = ActivatableReactions(faction);
        
        // Filter to only block reactions
        cardActivationOptions = cardActivationOptions
            .Where(cao => cao.CardState.CardLogic.IsBlockReaction)
            .ToList();
        
        if (cardActivationOptions != null && cardActivationOptions.Count > 0)
        {
            DebugUtilities.PrintPeer($"{faction} has {cardActivationOptions.Count} block reaction options");
            await Task.Delay(GameSettings.PauseDuration);
        }
        else
        {
            DebugUtilities.PrintPeer($"{faction} has no block reactions available");
            await Task.Delay(10);
        }
        
        return cardActivationOptions;
    }
    
    /// <summary>
    /// Request the initial card play from a faction (called by PlayStepHandlerDefault)
    /// This kicks off the play step for a faction
    /// </summary>
    public async static Task<CardActivationOption> RequestCardActivationOptions(Faction faction)
    {
        List<CardActivationOption> activationOptions = GetNextActions(faction);
        
        if (activationOptions != null && activationOptions.Count > 0)
        {
            CardActivationOption cardActivationOption = await GameSession.RequestPlay(faction);
            if (cardActivationOption != null)
            {
                DebugUtilities.PrintPeer($"{FactionState.ForEnum(faction).FactionLabel} is playing a card");
                await DoActivationOption(cardActivationOption);
                return cardActivationOption;
            }
        }
        else
        {
            DebugUtilities.PrintPeer($"{faction} does not have activation options");
        }
        
        return null;
    }
}
