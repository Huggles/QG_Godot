using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Manages a single card-play session (one faction's play step or start-turn step).
/// Instantiated fresh each time a faction enters a play step; tracks the active card pool,
/// change-event history, and reaction-chain orchestration for that session.
/// </summary>
public partial class CardPlayRound : GodotObject
{
    // ── Static accessor ────────────────────────────────────────────────────────
    public static CardPlayRound Current { get; private set; }

    // ── Instance state ─────────────────────────────────────────────────────────
    public List<CardState> CardPool { get; private set; } = new();
    public List<ChangeEvent> ChangeEventsPool { get; private set; } = new();
    public ChangeEvent LastChangeEvent { get; set; }
    private int reactionDepth = 0;

    // ── Computed properties ────────────────────────────────────────────────────
    public Dictionary<int, CardState> CardPoolMap =>
        CardPool.ToDictionary(c => c.Id, c => c);

    public Dictionary<int, ChangeEvent> ChangeEventsPoolMap =>
        ChangeEventsPool.ToDictionary(c => c.Id, c => c);

    public Faction LastChangeEventByFaction =>
        LastChangeEvent != null ? LastChangeEvent.TriggeringFaction : Faction.GERMANY;

    public FactionTeam LastChangeEventByTeam =>
        StaticGameData.OpponentFactionTeamForFaction(LastChangeEventByFaction);

    public ChangeEvent LastNoneNewCardChangeEvent =>
        ChangeEventsPool.LastOrDefault(c => c is not PlayCardChangeEvent && c is not ActivateReactionChangeEvent);

    public List<Faction> RequestOrder =>
        LastChangeEventByTeam == FactionTeam.AXIS ? AlliesFirstOrder : AxisFirstOrder;

    public static List<Faction> AxisFirstOrder => new()
        { Faction.GERMANY, Faction.JAPAN, Faction.ITALY, Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES };

    public static List<Faction> AlliesFirstOrder => new()
        { Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES, Faction.GERMANY, Faction.JAPAN, Faction.ITALY };

    // ── Entry point ────────────────────────────────────────────────────────────

    /// <summary>
    /// Start this round for the given faction. Assigns itself as Current, requests
    /// the faction's card play, and finishes when no more actions remain.
    /// Returns the step ID that was played, or -1 if the faction passed.
    /// </summary>
    public async Task<int> Start(Faction faction)
    {
        Current = this;
        int stepId = await RequestCardPlay(faction);
        if (stepId == -1)
        {
            Finish();
        }
        return stepId;
    }

    // ── Core pipeline ──────────────────────────────────────────────────────────

    /// <summary>
    /// Execute a card by card ID.
    /// Resolves the card's next executable step internally, handles the introduction event
    /// (PlayCard / ActivateReaction), and processes the resulting change event.
    /// </summary>
    public async Task DoCard(int cardId)
    {
        CardState cardState = CardState.ForId(cardId);
        if (cardState == null)
        {
            DebugUtilities.PrintPeerError($"DoCard: CardState {cardId} not found");
            return;
        }

        CardLogic cardLogic = cardState.CardLogic;

        reactionDepth++;
        bool isInitialPlay = reactionDepth == 1;

        // Step 1: Introduction event (may be blocked)
        bool introWasBlocked = false;

        if (!cardState.IsPlayed)
        {
            PlayCardChangeEvent introEvent = new PlayCardChangeEvent(cardState.Id);
            introEvent.IsTrigger = true;
            await ProcessIntroductionEvent(introEvent);
            introWasBlocked = introEvent.IsBlocked;
        }
        else
        {
            ActivateReactionChangeEvent introEvent = new ActivateReactionChangeEvent(cardState.Faction, cardState.Id, null);
            introEvent.IsTrigger = true;
            await ProcessIntroductionEvent(introEvent);
            introWasBlocked = introEvent.IsBlocked;
        }

        // Step 2: Execute the card's next unfinished step if not blocked.
        // We use all unfinished steps (not just executable ones) so that Execute() can
        // show the "Unable to" skip message for steps whose conditions fail before
        // automatically cascading to the next step.
        if (!introWasBlocked)
        {
            List<CardStep> allSteps = cardLogic.CardSteps;
            List<CardStep> nextSteps = allSteps.Where(s => !s.StepFinished).ToList();

            if (nextSteps.Count > 0)
            {
                ChangeEvent stepResult = await nextSteps[0].Execute();
                if (stepResult != null)
                    await DoChangeEvent(stepResult);
            }
        }

        reactionDepth--;

        // Step 3: If we're back to the top level, check whether the round is complete
        if (reactionDepth == 0 && isInitialPlay)
        {
            bool hasRemainingActions = RequestOrder.Any(f => GetAfterReactionOptions(f).Count > 0);
            if (!hasRemainingActions)
            {
                Finish();
            }
        }
    }

    /// <summary>
    /// Process a standard (non-introduction) change event through the full pipeline:
    /// add to pool → request blocks → apply if not blocked → request after reactions → continue multi-step cards.
    /// </summary>
    public async Task DoChangeEvent(ChangeEvent changeEvent)
    {
        if (changeEvent is PlayCardChangeEvent || changeEvent is ActivateReactionChangeEvent)
        {
            throw new InvalidOperationException(
                "DoChangeEvent must not be called for introduction events. Use DoCard instead.");
        }

        RegisterChangeEvent(changeEvent);

        if (changeEvent.IsTrigger)
        {
            await RequestBlockReactions(changeEvent);
        }

        if (!changeEvent.IsBlocked)
        {
            LastChangeEvent = changeEvent;
            DebugUtilities.PrintPeer($"Applying change event {changeEvent.ScriptName} from {FactionState.ForEnum(changeEvent.TriggeringFaction).FactionLabel}");
            await changeEvent.ApplyChange();
        }

        if (changeEvent.IsTrigger && !changeEvent.IsBlocked)
        {
            await RequestAfterReactions();
            await ContinueWithNextSteps();
        }
    }

    // ── Reaction chain helpers ─────────────────────────────────────────────────

    private async Task ProcessIntroductionEvent(ChangeEvent introEvent)
    {
        RegisterChangeEvent(introEvent);
        await RequestBlockReactions(introEvent);
        if (!introEvent.IsBlocked)
        {
            await introEvent.ApplyChange();
        }
    }

    private void RegisterChangeEvent(ChangeEvent changeEvent)
    {
        int sourceCardId = changeEvent.SourceCardId;
        if (sourceCardId > -1 && !CardPoolMap.ContainsKey(sourceCardId))
        {
            CardPool.Add(changeEvent.SourceCardState);
        }
        ChangeEventsPool.Add(changeEvent);
        LastChangeEvent = changeEvent;
    }

    private async Task RequestBlockReactions(ChangeEvent changeEvent)
    {
        foreach (Faction faction in RequestOrder)
        {
            if (faction == changeEvent.TriggeringFaction)
                continue; // Factions cannot block their own actions

            int cardId = await RequestBlock(faction);
            if (cardId != -1)
            {
                await DoCard(cardId);
            }
        }
    }

    private async Task RequestAfterReactions()
    {
        bool anyReactionPlayed = true;
        while (anyReactionPlayed)
        {
            anyReactionPlayed = false;
            foreach (Faction faction in RequestOrder)
            {
                List<int> options = GetAfterReactionOptions(faction);
                if (options.Count > 0)
                {
                    int cardId = await RequestPlay(faction);
                    if (cardId != -1)
                    {
                        DebugUtilities.PrintPeer($"AFTER REACTION from {FactionState.ForEnum(faction).FactionLabel}");
                        await DoCard(cardId);
                        anyReactionPlayed = true;
                        break; // Restart with updated RequestOrder
                    }
                }
            }
        }
    }

    private async Task ContinueWithNextSteps()
    {
        bool hasMoreSteps = true;
        while (hasMoreSteps)
        {
            hasMoreSteps = false;
            foreach (CardState cardState in CardPool)
            {
                if (cardState.CardLogic == null) continue;                
                if (cardState.CardLogic.ExecutableCardSteps.Count >0)
                {
                    DebugUtilities.PrintPeer($"Continuing with next step for {cardState.CardName}");
                    await DoCard(cardState.Id);
                    hasMoreSteps = true;
                    break; // Restart after processing one step
                }
            }
        }
    }

    // ── Player input requests ──────────────────────────────────────────────────

    /// <summary>Request the faction's initial card play, then execute the chosen card.</summary>
    public async Task<int> RequestCardPlay(Faction faction)
    {        
        int cardId = await RequestPlay(faction);
        if (cardId > -1)
        {
            DebugUtilities.PrintPeer($"{FactionState.ForEnum(faction).FactionLabel} is playing a card");
            await DoCard(cardId);
            return cardId;
        }
        return -1;
    }

    /// <summary>Ask the faction to choose a card to play/activate (initial play or after reaction).</summary>
    public async Task<int> RequestPlay(Faction faction)
    {
        PlayerScene controllingPlayer = PlayerFactionRegistry.GetPlayerSceneForFaction(faction);
        if (controllingPlayer == null)
        {
            DebugUtilities.PrintPeerError($"RequestPlay: No player found controlling {faction}");
            return -1;
        }

        int selectedId = -1;
        if(DeckState.ForFaction(faction).ActivatableCardIds.Count > 0)
        {
            InputRequest responseDto = await NetworkApi.Instance.SendInputRequest(new InputRequest.HandCardPlayRequestHandler(faction));       
            selectedId = responseDto.ResponseCardIds.Count > 0 ? responseDto.ResponseCardIds[0] : -1;
        }

        
        return selectedId;
    }

    /// <summary>Ask the faction to choose a block-reaction step, or pass.</summary>
    public async Task<int> RequestBlock(Faction faction)
    {
        PlayerScene controllingPlayer = PlayerFactionRegistry.GetPlayerSceneForFaction(faction);
        if (controllingPlayer == null)
        {
            DebugUtilities.PrintPeerError($"RequestBlock: No player found controlling {faction}");
            return -1;
        }

        // Factions cannot block their own events
        if (LastChangeEvent?.TriggeringFaction == faction)
            return -1;

        bool hasBlockOptions = GameSession.Current.GameState.CardStatesById.Values
            .Any(cs => cs.Tags.Has(Tag.IsBlockReaction, faction));

        if (!hasBlockOptions)
        {
            DebugUtilities.PrintPeer($"{faction} has no block reactions available");
            await Task.Delay(10);
            return -1;
        }

        DebugUtilities.PrintPeer($"{faction} has block reaction options");
        await Task.Delay(GameSettings.DurationMedium);

        controllingPlayer.InputManager.SetPlayCardInputActive(faction);
        Variant[] results = await EventBus.GetSignalAwaiter("CardSelected");
        if (results == null || results.Length == 0)
            return -1;

        return (int)results[0];
    }

    /// <summary>Card IDs of hand cards the faction can play from hand.</summary>
    public List<int> PlayableCardIds(Faction faction) => ActivatableCardIds(faction).Where(id => CardState.ForId(id).IsPlayed == false).ToList();

    public List<int> PlayableHandCardIds(Faction faction) => PlayableCardIds(faction).Intersect(DeckState.ForFaction(faction).HandCardIds).ToList();

    /// <summary>Card IDs of status/response cards the faction can activate now.</summary>
    public List<int> ActivatableCardIds(Faction faction) => CardState.AllForFaction(faction).Values.Where(cs => cs.Tags.Has(Tag.IsActivatable, faction)).Select(cs => cs.Id).ToList();

    /// <summary>Card IDs of after-reactions (non-block) available to the faction.</summary>
    public List<int> GetAfterReactionOptions(Faction faction) => CardState.AllForFaction(faction).Values.Where(cs => cs.Tags.Has(Tag.IsAfterReaction, faction)).Select(cs => cs.Id).ToList();

    public List<T> GetChangeEvents<T>() where T : ChangeEvent
    {
        return ChangeEventsPool.OfType<T>().ToList();
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Finish()
    {
        DebugUtilities.PrintPeer("CardPlayRound: finished");
        Current = null;
        EventBus.Emit(EventBus.SignalName.CardPlayPoolFinished);
    }

    public void ClearPool()
    {
        CardPool.Clear();
        ChangeEventsPool.Clear();
        LastChangeEvent = null;
        reactionDepth = 0;
        Current = null;
    }

    // ── Private helpers ────────────────────────────────────────────────────────
}
