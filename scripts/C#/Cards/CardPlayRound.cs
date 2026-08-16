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

    /// <summary>Create a fresh CardPlayRound, register it as Current, and return it.</summary>
    public static CardPlayRound StartNew()
    {
        Current = new CardPlayRound();
        return Current;
    }

    // ── Instance state ─────────────────────────────────────────────────────────
    public List<CardState> CardPool { get; private set; } = new();
    public List<ChangeEvent> ChangeEventsPool { get; private set; } = new();
    public ChangeEvent LastChangeEvent { get; set; }
    public int ReactionDepth { get; private set; } = 0;
    private HashSet<Faction> _afterReactionPassedFactions = new();

    /// <summary>
    /// Game-loop epoch this round belongs to, captured at construction. Error recovery bumps the
    /// epoch, so a continuation that resumes from an await after a recovery can compare against this
    /// and unwind instead of mutating state alongside the resumed loop.
    /// </summary>
    private readonly int epoch = ErrorReporter.GameLoopEpoch;

    /// <summary>Throws <see cref="AbortedEpochException"/> if this round has been superseded by a recovery.</summary>
    private void ThrowIfAborted() => ErrorReporter.ThrowIfStaleEpoch(epoch);

    /// <summary>
    /// The change event that opened the currently-active reaction window.
    /// Set before each <see cref="RequestAfterReactions"/> call and restored afterwards,
    /// so card step logic can determine which specific event they are reacting to.
    /// </summary>
    public ChangeEvent CurrentReactionTrigger { get; private set; }

    /// <summary>
    /// The change event currently being evaluated for blocking.
    /// Set for the duration of <see cref="RequestBlockReactions"/> so the UI can
    /// show the faction what they are being asked to block.
    /// </summary>
    public ChangeEvent CurrentBlockTrigger { get; private set; }

    // ── Computed properties ────────────────────────────────────────────────────
    public Dictionary<int, CardState> CardPoolMap =>
        CardPool.ToDictionary(c => c.Id, c => c);

    public Dictionary<int, ChangeEvent> ChangeEventsPoolMap =>
        ChangeEventsPool.ToDictionary(c => c.Id, c => c);

    public Faction LastChangeEventByFaction =>
        LastChangeEvent != null ? LastChangeEvent.TriggeringFaction : Faction.GERMANY;

    public FactionTeam LastChangeEventByTeam => StaticGameData.FactionTeamForFaction(LastChangeEventByFaction);

    public ChangeEvent LastNoneNewCardChangeEvent => ChangeEventsPool.LastOrDefault(c => c is not PlayCardChangeEvent && c is not ActivateReactionChangeEvent);

    public List<Faction> RequestOrder => LastChangeEventByTeam == FactionTeam.AXIS ? AlliesFirstOrder : AxisFirstOrder;

    public static List<Faction> AxisFirstOrder => new()
        { Faction.GERMANY, Faction.JAPAN, Faction.ITALY, Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES };

    public static List<Faction> AlliesFirstOrder => new()
        { Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES, Faction.GERMANY, Faction.JAPAN, Faction.ITALY };

    // ── Entry point ────────────────────────────────────────────────────────────

    /// <summary>
    /// Start this round for the given faction. Assigns itself as Current, requests
    /// the faction's card play, and finishes when no more actions remain.
    /// During the start turn step, loops to allow multiple card activations.
    /// Returns the last step ID that was played, or -1 if the faction passed immediately.
    /// </summary>
    public async Task<int> Start(Faction faction)
    {
        Current = this;
        bool isStartTurnStep = GameFlow.Instance.TurnStep == TurnStep.START;
        int lastStepId = -1;

        while (true)
        {
            int stepId = await RequestCardPlay(faction);
            if (stepId == -1)
            {
                break;
            }
            
            lastStepId = stepId;

            // During the start turn step, keep looping to allow multiple activations.
            // During any other step, one card play ends the round.
            if (!isStartTurnStep)
            {
                break;
            }
        }

        Finish();
        return lastStepId;
    }

    // ── Core pipeline ──────────────────────────────────────────────────────────

    /// <summary>
    /// Execute a card by card ID.
    /// Resolves the card's next executable step internally, handles the introduction event
    /// (PlayCard / ActivateReaction), and processes the resulting change event.
    /// </summary>
    /// <param name="activationTrigger">
    /// The event this activation is reacting to — the event being offered for block, or the event
    /// that opened the after-reaction window. Bound onto the CardLogic for the duration of the
    /// activation so step logic has a stable handle on it (see CardLogic.ActivationTrigger).
    /// Passed explicitly rather than inferred from CurrentBlockTrigger/CurrentReactionTrigger:
    /// inside an after-reaction window nested in a block window both are set, and neither
    /// precedence rule is reliably correct. Null for a card played from hand outside a reaction.
    /// </param>
    public async Task DoCard(int cardId, ChangeEvent activationTrigger = null)
    {
        // If the loop was recovered while this call was queued behind an await, this continuation
        // belongs to an aborted pipeline. Unwind rather than run alongside the resumed loop.
        ThrowIfAborted();

        CardState cardState = CardState.ForId(cardId);
        if (cardState == null)
        {
            DebugUtilities.PrintPeerError($"DoCard: CardState {cardId} not found");
            return;
        }

        CardLogic cardLogic = cardState.CardLogic;

        ReactionDepth++;
        bool isInitialPlay = ReactionDepth == 1;

        // Captured BEFORE the introduction event: PlayCardChangeEvent -> DeckState.PlayCard moves
        // the card into the Status/Response pile, which flips IsPlayed.
        // Playing a Status/Response card onto the table IS the play — it has no immediate effect.
        // Its CardSteps are the activation effect and must wait for the card's trigger to fire.
        bool isTableCardPlay = cardLogic.IsTableCardInHand;

        // Bind the trigger BEFORE the introduction event: that event opens its own block and
        // after-reaction windows, and anything played in them registers change events onto the pool.
        // Skipped for a table-card play — putting a Status/Response card down is not an activation;
        // its steps run later, when its trigger actually fires, and must bind then.
        // Saved/restored rather than assigned so a nested activation of the same CardLogic instance
        // cannot clobber an outer binding.
        ChangeEvent previousActivationTrigger = cardLogic.ActivationTrigger;
        if (activationTrigger != null && !isTableCardPlay)
            cardLogic.ActivationTrigger = activationTrigger;

        try
        {
            // Step 1: Introduction event 
            ChangeEvent introEvent = !cardState.IsPlayed
                ? new PlayCardChangeEvent(cardState.Id)
                : new ActivateReactionChangeEvent(cardState.Faction, cardState.Id, activationTrigger);
            introEvent.IsTrigger = false;
            await introEvent.Apply();        

            //We only let the faction that played the card react to the intro event, since it is the only one that can play a reaction to it.
            //Any block of the entire card happens during the blockRequest of the first step of the card. 
            //This makes sure we are not doing an extra reaction window for the intro event, which is not a trigger and cannot be blocked.
            _afterReactionPassedFactions.Clear();
            if(introEvent is PlayCardChangeEvent)
                await RequestAfterReactions(introEvent, new List<Faction>{cardState.Faction});
            

            // Step 2: Execute the card's next unfinished step.
            // We use all unfinished steps (not just executable ones) so that Execute() can
            // show the "Unable to" skip message for steps whose conditions fail before
            // automatically cascading to the next step.
            
            // Activation window: fires immediately after the card is activated/played,
            // before its own steps execute. CurrentReactionTrigger is set to introEvent so
            // .Immediately() conditions like CardActivated (e.g. ResponseEnigmaCodeCracked
            // discarding the activated card) fire here, ahead of any reactions to the change
            // events this card's own steps are about to produce.

            // ContinueWithNextSteps is intentionally not called here: this card's own steps
            // are resumed by the loop right below, and any change event a triggered reaction
            // produces already gets its own continuation handling via its nested DoCard call.           

            // A Status/Response card just played from hand stops here: it sits on the table until
            // its trigger fires, at which point it re-enters DoCard on the activation branch.
            if (!isTableCardPlay)
            {
                List<CardStep> allSteps = cardLogic.CardSteps;
                while (allSteps.Where(s => !s.StepFinished).ToList().Count > 0 && cardLogic.IsBlocked == false)
                {
                    List<CardStep> nextSteps = allSteps.Where(s => !s.StepFinished ).ToList();
                    ChangeEvent stepResult = await nextSteps[0].Execute();
                    //Only apply the change event if it is not null, if the step is not blocked, and the card is not blocked.
                    if (stepResult != null && stepResult.IsBlocked && !cardLogic.IsBlocked)
                        await DoChangeEvent(stepResult);
                }
            }
        }
        finally
        {
            // Released only once every step is done: a card whose remaining steps are resumed later
            // by ContinueWithNextSteps must keep the event it originally reacted to.
            if (cardLogic.CardSteps.All(s => s.StepFinished))
                cardLogic.ActivationTrigger = previousActivationTrigger;
        }

        ReactionDepth--;
    }

    /// <summary>
    /// Process a standard (non-introduction) change event through the full pipeline:
    /// add to pool → request blocks → apply if not blocked → request after reactions → continue multi-step cards.
    /// </summary>
    public async Task DoChangeEvent(ChangeEvent changeEvent)
    {
        ThrowIfAborted();

        if (changeEvent is PlayCardChangeEvent || changeEvent is ActivateReactionChangeEvent)
        {
            throw new InvalidOperationException(
                "DoChangeEvent must not be called for introduction events. Use DoCard instead.");
        }

        RegisterChangeEvent(changeEvent);

        if (changeEvent.IsTrigger)
        {
            List<Faction> factions = StaticGameData.OpponentFactionsForTeam(StaticGameData.FactionTeamForFaction(changeEvent.TriggeringFaction));
            await RequestBlockReactions(changeEvent, factions);
        }

        if (!changeEvent.IsBlocked)
        {
            LastChangeEvent = changeEvent;
            DebugUtilities.PrintPeer($"Applying change event {changeEvent.ScriptName} from {FactionState.ForEnum(changeEvent.TriggeringFaction).FactionLabel}");
            await changeEvent.Apply();
            DebugUtilities.PrintPeer($"Finished applying change event {changeEvent.ScriptName} from {FactionState.ForEnum(changeEvent.TriggeringFaction).FactionLabel}");
        }

        if (changeEvent.IsTrigger && !changeEvent.IsBlocked)
        {
            DebugUtilities.PrintPeer($"Requesting after-reactions for change event {changeEvent.ScriptName} from {FactionState.ForEnum(changeEvent.TriggeringFaction).FactionLabel}");
            bool anyReactionPlayed = await RequestAfterReactions(changeEvent);
            if (anyReactionPlayed)
                await ContinueWithNextSteps();
        }
        DebugUtilities.PrintPeer($"Finished processing change event {changeEvent.ScriptName} from {FactionState.ForEnum(changeEvent.TriggeringFaction).FactionLabel}");
    }

    // ── Reaction chain helpers ─────────────────────────────────────────────────   

    public void RegisterChangeEvent(ChangeEvent changeEvent)
    {
        int sourceCardId = changeEvent.SourceCardId;
        if (sourceCardId > -1 && !CardPoolMap.ContainsKey(sourceCardId))
        {
            CardPool.Add(changeEvent.SourceCardState);
        }
        ChangeEventsPool.Add(changeEvent);
        LastChangeEvent = changeEvent;
    }

    private async Task RequestBlockReactions(ChangeEvent changeEvent, List<Faction> blockRequestOrder = null)
    {
        // Saved and restored rather than nulled, mirroring CurrentReactionTrigger. A block card
        // played below opens a nested window via its own introduction event; when that returns,
        // this window's remaining factions must get their trigger identity back — block trigger
        // conditions, the self-block guard and the prompt's trigger label all read it.
        ChangeEvent previousBlockTrigger = CurrentBlockTrigger;
        CurrentBlockTrigger = changeEvent;
        try
        {
            // Recalculated inside the loop rather than once before it. Block trigger conditions
            // read CurrentBlockTrigger, so any tags computed before the assignment above describe
            // a different window — and a block card played by an earlier faction runs its own
            // nested windows and steps, each of which recalculates under a different trigger.
            // tagsDirty keeps this at one call per window in the common case.
            bool tagsDirty = true;
            foreach (Faction faction in blockRequestOrder ?? RequestOrder)
            {
                if (changeEvent.IsBlocked)
                    break; // Already prevented — there is nothing left for anyone to block

                if (faction == changeEvent.TriggeringFaction)
                    continue; // Factions cannot block their own actions

                if (tagsDirty)
                {
                    GameStateCalculator.CalculateAll();
                    tagsDirty = false;
                }

                int cardId = await RequestBlock(faction);
                if (cardId != -1)
                {
                    await DoCard(cardId, changeEvent);
                    tagsDirty = true;
                }
            }
        }
        finally
        {
            CurrentBlockTrigger = previousBlockTrigger;
        }
    }

    private async Task<bool> RequestAfterReactions(ChangeEvent triggerEvent, List<Faction> reactionRequestOrder = null)    
    {
        DebugUtilities.PrintPeer("RequestAfterReactions");
        var previousTrigger = CurrentReactionTrigger;
        CurrentReactionTrigger = triggerEvent;
        // Scope the pass-tracking set to this reaction window, mirroring CurrentReactionTrigger.
        // Each nested window gets its own set so a faction that passes on an inner trigger (e.g.
        // skipping Synthetic Fuel on a deploy) is not wrongly treated as having passed on the
        // outer trigger (e.g. still being owed a Dive Bombers offer on the original battle).
        var previousPassed = _afterReactionPassedFactions;
        _afterReactionPassedFactions = new HashSet<Faction>();
        bool anyEverPlayed = false;

        // A card can never react to its own introduction. DeckState.PlayCard files a Response card
        // into ResponseCardIds with IsRevealed = false BEFORE this window opens, so without this the
        // player is prompted in the one window where the card they just played is provably
        // unplayable — and, if it was their only hidden Response, prompted with nothing else to
        // offer. Excluded from the offered options and from the always-ask cover count alike.
        int excludedCardId = triggerEvent is PlayCardChangeEvent or ActivateReactionChangeEvent
            ? triggerEvent.SourceCardId
            : -1;

        try
        {
            bool anyPlayedThisPass = true;
            while (anyPlayedThisPass)
            {
                // Recalculate tags at the start of every pass so Immediate-scope
                // EventConditions always evaluate against the current (possibly restored)
                // CurrentReactionTrigger — including after a nested reaction card resolves
                // and the trigger is restored from the finally block.
                GameStateCalculator.CalculateAll();
                anyPlayedThisPass = false;
                foreach (Faction faction in reactionRequestOrder ?? RequestOrder)
                {
                    if (_afterReactionPassedFactions.Contains(faction)) continue;
                    List<int> options = GetAfterReactionOptions(faction);
                    options.Remove(excludedCardId);
                    // A faction the gate declines is re-evaluated next pass rather than recorded as
                    // having passed, exactly as a faction with no options always has been — the gate
                    // is a local state read, so there is no round-trip to save by caching it.
                    if (!ShouldOpenReactionWindow(faction, options, excludedCardId)) continue;

                    int cardId = await RequestPlay(faction, options);
                    if (cardId != -1)
                    {
                        DebugUtilities.PrintPeer($"AFTER REACTION from {FactionState.ForEnum(faction).FactionLabel}");
                        _afterReactionPassedFactions.Clear(); // A reaction is about to happen — give everyone a fresh chance
                        await DoCard(cardId, triggerEvent);
                        anyEverPlayed = true;
                        anyPlayedThisPass = true;
                        break; // Restart with updated RequestOrder
                    }
                    else
                    {
                        _afterReactionPassedFactions.Add(faction);
                    }
                }
            }
            DebugUtilities.PrintPeer("No more after-reaction options available for any faction");
        }
        finally
        {
            CurrentReactionTrigger = previousTrigger;
            _afterReactionPassedFactions = previousPassed;
        }
        return anyEverPlayed;
    }

    private async Task ContinueWithNextSteps()
    {
        DebugUtilities.PrintPeer("ContinueWithNextSteps");
        bool hasMoreSteps = true;
        while (hasMoreSteps)
        {
            hasMoreSteps = false;
            foreach (CardState cardState in CardPool.AsEnumerable().Reverse())
            {
                if (cardState.CardLogic == null) continue;                                
                if (cardState.CardLogic.ExecutableCardSteps.Count > 0 && cardState.IsBlocked == false)
                {
                    DebugUtilities.PrintPeer($"Continuing with next step for {cardState.CardName}");
                    _afterReactionPassedFactions.Clear(); // New step = fresh reaction window for all factions
                    await DoCard(cardState.Id);
                    hasMoreSteps = true;
                    break; // Restart after processing one step
                }
            }
        }
        DebugUtilities.PrintPeer("No more executable steps available for any card in the pool");
    }

    // ── Player input requests ──────────────────────────────────────────────────

    /// <summary>Request the faction's initial card play, then execute the chosen card.</summary>
    public async Task<int> RequestCardPlay(Faction faction)
    {        
        // Recalculate at depth 0 so purely state-based cards (e.g. start-step status cards)
        // are correctly reflected in ActivatableCardIds for subsequent START-step iterations.
        GameStateCalculator.CalculateAll();
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
    /// <param name="reactionOptions">
    /// Non-null when this is an after-reaction window: the exact set of cards on offer, sent to the
    /// controlling peer as TargetCardIds the same way RequestBlock sends its block options.
    ///
    /// Without it the request fell through to HandCardPlayRequestHandler for any faction that had not
    /// yet played a hand card this turn step — which is every faction reacting on an opponent's turn —
    /// and that handler deliberately offers the whole hand. The prompt then showed all of the reacting
    /// faction's hand cards beside the event it was answering. Hand cards are never playable inside a
    /// reaction window, so the reaction path must never build that request.
    /// </param>
    public async Task<int> RequestPlay(Faction faction, List<int> reactionOptions = null)
    {
        PlayerScene controllingPlayer = PlayerFactionRegistry.GetPlayerSceneForFaction(faction);
        if (controllingPlayer == null)
        {
            DebugUtilities.PrintPeerError($"RequestPlay: No player found controlling {faction}");
            return -1;
        }

        bool isReaction = reactionOptions != null;
        // In a reaction window the caller has already run ShouldOpenReactionWindow, and an empty
        // option list is the whole point of the always-ask rule — an unanswerable prompt is the cover
        // that stops the prompt itself from revealing a face-down Response card.
        bool hasOptions = isReaction || DeckState.ForFaction(faction).ActivatableCardIds.Count > 0;

        int selectedId = -1;
        if(hasOptions)
        {
            InputRequest request;
            if (isReaction)
            {
                // Carried explicitly rather than re-derived on the client. The client never runs
                // GameStateCalculator (tags arrive over the wire) and has no CardPlayRound, so it
                // cannot reproduce the host's reaction-depth-aware filtering on its own.
                request = new InputRequest.ActivateCardRequestHandler(faction)
                {
                    TargetCardIds = reactionOptions,
                    DisplayCardIds = ReactionWindowDisplayCardIds(faction),
                    IsReactionWindow = true,
                };
            }
            else
            {
                // Only this faction's own plays consume its hand-card play for the turn step; another
                // faction playing must not hide this faction's hand cards.
                bool hasPlayedHandCardThisTurnStep =
                    GameFlow.Instance.CardsPlayedThisTurnStep.TryGetValue(faction, out int cardsPlayedByFaction)
                    && cardsPlayedByFaction > 0;
                bool isStartTurnStep = GameFlow.Instance.TurnStep == TurnStep.START;
                request = hasPlayedHandCardThisTurnStep || isStartTurnStep
                    ? new InputRequest.ActivateCardRequestHandler(faction)
                    : new InputRequest.HandCardPlayRequestHandler(faction);
            }

            if (CurrentReactionTrigger != null)
            {
                request.TriggerCardId = GetTriggerCardId(CurrentReactionTrigger);
                request.TriggerSummaryText = CurrentReactionTrigger.SummaryText();
            }

            InputRequest responseDto = await NetworkApi.Instance.SendInputRequest(request);
            if (isReaction)
                GameFlow.Instance.RecordReactionSkip(faction, responseDto.ReactionSkipScope);
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

        // Factions cannot block their own events. Read from CurrentBlockTrigger, not LastChangeEvent:
        // once a nested window has run, LastChangeEvent is no longer this window's event.
        if (CurrentBlockTrigger?.TriggeringFaction == faction)
            return -1;

        List<int> blockOptions = GetBlockReactionOptions(faction);

        if (!ShouldOpenReactionWindow(faction, blockOptions))
        {
            DebugUtilities.PrintPeer($"{faction} is not offered a block window (no block reactions, and nothing hidden to cover for)");
            await Task.Delay(10);
            return -1;
        }

        DebugUtilities.PrintPeer($"{faction} has block reaction options: {string.Join(",", blockOptions)}");

        // Route through the network seam (like RequestPlay) so the authoritative host — which may
        // be a faction-less headless server — awaits the controlling peer's response instead of a
        // local UI click. The client's Handle() runs the actual card-selection UI.
        InputRequest.BlockReactionRequestHandler request = new InputRequest.BlockReactionRequestHandler(faction)
        {
            // Sent explicitly so the client prompt offers only block-eligible cards rather than
            // everything tagged IsActivatable. Tag.IsBlockReaction itself stays server-internal.
            TargetCardIds = blockOptions,
            DisplayCardIds = ReactionWindowDisplayCardIds(faction),
            TriggerCardId = GetTriggerCardId(CurrentBlockTrigger),
            TriggerSummaryText = CurrentBlockTrigger?.SummaryText()
        };

        InputRequest responseDto = await NetworkApi.Instance.SendInputRequest(request);
        GameFlow.Instance.RecordReactionSkip(faction, responseDto.ReactionSkipScope);
        return responseDto.ResponseCardIds.Count > 0 ? responseDto.ResponseCardIds[0] : -1;
    }

    /// <summary>Card IDs of hand cards the faction can play from hand.</summary>
    public List<int> PlayableCardIds(Faction faction) => ActivatableCardIds(faction).Where(id => CardState.ForId(id).IsPlayed == false).ToList();

    public List<int> PlayableHandCardIds(Faction faction) => PlayableCardIds(faction).Intersect(DeckState.ForFaction(faction).HandCardIds).ToList();

    /// <summary>Card IDs of status/response cards the faction can activate now.</summary>
    public List<int> ActivatableCardIds(Faction faction) => CardState.AllForFaction(faction).Values.Where(cs => cs.Tags.Has(Tag.IsActivatable, faction)).Select(cs => cs.Id).ToList();

    /// <summary>Card IDs of after-reactions (non-block) available to the faction.</summary>
    public List<int> GetAfterReactionOptions(Faction faction) => CardState.AllForFaction(faction).Values.Where(cs => cs.Tags.Has(Tag.IsAfterReaction, faction)).Select(cs => cs.Id).ToList();

    /// <summary>Card IDs of block reactions available to the faction.</summary>
    public List<int> GetBlockReactionOptions(Faction faction) => CardState.AllForFaction(faction).Values.Where(cs => cs.Tags.Has(Tag.IsBlockReaction, faction)).Select(cs => cs.Id).ToList();

    /// <summary>
    /// True if the faction holds at least one face-down Response card on the table.
    ///
    /// Such a faction is offered a reaction window even with nothing activatable. Asking only when a
    /// reaction is actually available makes the mere appearance of the prompt proof that the hidden
    /// card reacts to exactly this event — and its absence proof that it does not. The empty prompt
    /// is the cover story; the player passes with Skip.
    ///
    /// <paramref name="excludeCardId"/> drops one card from the count: inside an introduction
    /// event's window the card being introduced cannot react to itself, so it must not be the reason
    /// a cover window opens. The residual tell — that being prompted there implies you hold ANOTHER
    /// hidden Response card — is accepted. It is a count, not an identity, and the alternative is
    /// asking a question the player provably cannot answer.
    /// </summary>
    public static bool HasHiddenResponseCards(Faction faction, int excludeCardId = -1) =>
        DeckState.ForFaction(faction).ResponseCardIds
            .Any(id => id != excludeCardId && CardState.ForId(id)?.IsRevealed == false);

    /// <summary>
    /// Every event-triggered card the faction has on the table: the full set a reaction prompt puts
    /// in front of the player, whether or not each one can be played right now. The prompt then
    /// greys out and un-clicks everything outside <see cref="GetAfterReactionOptions"/> /
    /// <see cref="GetBlockReactionOptions"/>.
    ///
    /// Showing only the playable cards left the player guessing why a card they knew they held was
    /// absent — and an always-ask window showed an empty table, which reads as a bug rather than as
    /// "nothing of yours triggers here". Both piles are on-table only: DeckState.DiscardCard removes
    /// a spent card from them.
    ///
    /// Passive-modifier Status cards are excluded. HasEventBasedTrigger is the same property
    /// CanBeActivated uses to keep them out of reaction chains, so a card that could never be a
    /// reaction is not shown as one that merely is not available.
    /// </summary>
    public static List<int> ReactionWindowDisplayCardIds(Faction faction)
    {
        DeckState deck = DeckState.ForFaction(faction);
        return deck.StatusCardIds
            .Concat(deck.ResponseCardIds)
            .Where(id => CardState.ForId(id)?.CardLogic?.HasEventBasedTrigger == true)
            .ToList();
    }

    /// <summary>
    /// True when every player can already see what this table card is: a Status card sits face up,
    /// and a Response card stays face up once an earlier activation revealed it. A window opened by
    /// such a card leaks nothing whether it appears or not.
    /// </summary>
    private static bool IsPubliclyVisibleTableCard(int cardId)
    {
        CardState card = CardState.ForId(cardId);
        return card != null && (card.CardData.CardType != CardType.RESPONSE || card.IsRevealed);
    }

    /// <summary>
    /// Whether to open a reaction window for this faction, given the reactions it can actually play.
    /// The single gate for both the block and the after-reaction path.
    ///
    /// <paramref name="excludeCardId"/> is the card being introduced, when this is an introduction
    /// event's window — it can neither be offered nor act as cover. The caller has already stripped
    /// it from <paramref name="options"/>; this passes it on to the cover count. Block windows leave
    /// it at -1: introduction events no longer open one, so there is nothing to exclude.
    /// </summary>
    private static bool ShouldOpenReactionWindow(Faction faction, List<int> options, int excludeCardId = -1)
    {
        // A scoped skip only silences the windows that exist to hide information. A face-up Status
        // card (or an already-revealed Response) is public knowledge, so the player keeps that
        // window — only the ones that exist solely because of face-down cards go away.
        if (GameFlow.Instance.IsSkippingReactions(faction))
            return options.Any(IsPubliclyVisibleTableCard);

        return options.Count > 0 || HasHiddenResponseCards(faction, excludeCardId);
    }

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
        // The round's change events are about to go away, so no card may keep pointing at one.
        CardPool.ForEach(cardState => { if (cardState.CardLogic != null) cardState.CardLogic.ActivationTrigger = null; });
        CardPool.Clear();
        ChangeEventsPool.Clear();
        LastChangeEvent = null;
        ReactionDepth = 0;
        Current = null;
        _afterReactionPassedFactions.Clear();
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the card ID to display for a trigger event.
    /// Prefers the event's own source card; falls back to the last card in the pool.
    /// </summary>
    private int GetTriggerCardId(ChangeEvent trigger)
    {
        if (trigger?.SourceCardId > -1) return trigger.SourceCardId;
        return CardPool.LastOrDefault()?.Id ?? -1;
    }
}
