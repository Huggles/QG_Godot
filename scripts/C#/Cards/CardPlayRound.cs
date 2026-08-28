using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

    public ChangeEvent LastNoneNewCardChangeEvent => ChangeEventsPool.LastOrDefault(c => c is not PlayCardChangeEvent && c is not ActivateReactionChangeEvent);

    // A flat six-faction RequestOrder used to live here, derived from LastChangeEvent. It is gone:
    // the rules define no order between the factions of a team, only an order between the two TEAMS,
    // and deriving that from LastChangeEvent read a live pool value that every nested reaction moved.
    // Each reaction window now owns its own turn pointer — see RequestAfterReactions.

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

            // An introduction event opens NO reaction window of its own. It is not a trigger, so it
            // cannot be blocked, and it no longer opens an after-reaction window either: the
            // "activation window" that used to run here cost two GameStateCalculator.CalculateAll()
            // passes — each broadcasting a RecalculateTagsMessage to every peer — plus a blocking
            // prompt, on every single card play, to serve two cards.
            //
            // Those cards now reach the windows the card's own resolution already opens:
            //   - Reacting to the card being PLAYED (ResponseRationing, StatusWomenConscripts):
            //     Condition.FactionPlayedCard scans the round's event pool, so they are offered in the
            //     after-reaction window of any step of the played card.
            //   - Reacting to the card being ACTIVATED, before its effect lands
            //     (ResponseEnigmaCodeCracked): a real block reaction on the first blockable step, via
            //     Condition.IsBlockRequestFromCard.
            // Consequence, accepted: a play whose steps open no window at all — conditions unmet, the
            // player cancels the selection, or the step's only event is IsTrigger = false — offers no
            // reaction to it either. A card added with an IsTrigger = false first step silently takes
            // that away from these reactions.
            _afterReactionPassedFactions.Clear();

            // Step 2: Execute the card's next unfinished step.
            // We use all unfinished steps (not just executable ones) so that Execute() can
            // show the "Unable to" skip message for steps whose conditions fail before
            // automatically cascading to the next step.

            // A Status/Response card just played from hand stops here: it sits on the table until
            // its trigger fires, at which point it re-enters DoCard on the activation branch.
            if (!isTableCardPlay)
            {
                List<CardStep> allSteps = cardLogic.CardSteps;
                while (allSteps.Where(s => !s.StepFinished).ToList().Count > 0 && cardLogic.IsBlocked == false)
                {
                    List<CardStep> nextSteps = allSteps.Where(s => !s.StepFinished ).ToList();
                    await nextSteps[0].Execute();
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

        // Which team is offered the block is RequestBlockReactions' own business now — it is the
        // opponent team in every case, so there is nothing for the caller to decide.
        if (changeEvent.IsTrigger)
            await RequestBlockReactions(changeEvent);

        if (!changeEvent.IsBlocked)
        {
            // After the block window, before Apply(): a faction whose pool is empty must free a unit
            // first, but only once the deploy is actually going to happen. See UnitPoolShortfall.
            if (changeEvent is DeployUnitChangeEvent deployEvent)
                await UnitPoolShortfall.ResolveBeforeDeploy(deployEvent);

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

    /// <summary>
    /// Take one team's turn in a reaction window: prompt every candidate faction at the same time and
    /// return the first card any of them chooses.
    ///
    /// A team uses only one reaction per turn (reaction-specifics), so the first card chosen ends the
    /// turn and every prompt still open is withdrawn. Which of its reactions a team uses, and in what
    /// order, is the team's own decision — the rules define no order between the factions of a team,
    /// so racing them IS the choice rather than an approximation of one. The flat six-faction
    /// RequestOrder this replaced invented one (UK always before Soviet before US).
    ///
    /// Grouped by controlling peer, the same shape <see cref="OpeningDiscard"/> uses: groups run
    /// concurrently, the factions inside a group one after another. A peer is only ever asked one thing
    /// at a time, so its own factions must not be prompted simultaneously — and a single-process game
    /// (the CLI, a hotseat) collapses to one group and stays fully sequential.
    /// </summary>
    /// <param name="ask">Raises one faction's prompt. Given the turn's token so it can be withdrawn.</param>
    /// <param name="passed">
    /// Factions that declined are added here. A faction whose prompt was withdrawn, or that was never
    /// reached because the turn was already decided, is deliberately NOT added: it made no decision,
    /// and must still be offered when this team's turn comes round again.
    /// </param>
    /// <returns>The winning faction and card id, or <c>(Faction.NONE, -1)</c> if the whole team declined.</returns>
    private static async Task<(Faction faction, int cardId)> TakeTeamTurn(
        List<Faction> candidates,
        Func<Faction, CancellationToken, Task<int>> ask,
        HashSet<Faction> passed)
    {
        if (candidates.Count == 0) return (Faction.NONE, -1);

        using CancellationTokenSource turnDecided = new();
        object winnerLock = new();
        Faction winningFaction = Faction.NONE;
        int winningCardId = -1;

        List<Task> groups = candidates
            .GroupBy(PlayerFactionRegistry.GetPeerIdForFaction)
            .Select(async peerFactions =>
            {
                foreach (Faction faction in peerFactions)
                {
                    // Checked before the prompt is raised, not only inside the wait: once the turn is
                    // decided this peer's later factions must not reach the screen at all.
                    if (turnDecided.IsCancellationRequested) return;

                    int cardId = await ask(faction, turnDecided.Token);

                    // Withdrawn rather than answered — no decision to record either way.
                    if (turnDecided.IsCancellationRequested)
                    {
                        if (cardId != -1)
                            DebugUtilities.PrintPeer(
                                $"{faction} chose card {cardId} just as the team turn was decided elsewhere — " +
                                "discarded; it is offered again on this team's next turn");
                        return;
                    }

                    if (cardId == -1)
                    {
                        lock (winnerLock) passed.Add(faction);
                        continue;
                    }

                    lock (winnerLock)
                    {
                        if (winningCardId != -1) return;
                        winningFaction = faction;
                        winningCardId = cardId;
                    }
                    turnDecided.Cancel();
                    return;
                }
            })
            .ToList();

        // Every prompt in this turn must be closed before the caller resolves the winning card.
        // Resolving while another peer is still deciding would let DoCard mutate state alongside a
        // live prompt, and the peers' GameMessage ids and state hashes would stop agreeing — the same
        // hazard OpeningDiscard gathers in parallel but applies serially to avoid.
        await Task.WhenAll(groups);

        return (winningFaction, winningCardId);
    }

    /// <summary>
    /// The team that reacts first to <paramref name="triggerEvent"/>: the one that did not cause it.
    ///
    /// A trigger with no faction behind it is a game mechanic rather than a country's action. Per
    /// reaction-specifics the team of the faction taking its turn goes SECOND there, so the causing
    /// team is read from GameFlow instead of from the event.
    /// </summary>
    private static FactionTeam FirstTeamToReactTo(ChangeEvent triggerEvent)
    {
        FactionTeam causedBy = StaticGameData.FactionTeamForFaction(triggerEvent.TriggeringFaction);
        if (causedBy == FactionTeam.NONE)
            causedBy = StaticGameData.FactionTeamForFaction(GameFlow.Instance.CurrentFaction);
        return StaticGameData.OpponentTeam(causedBy);
    }

    private async Task RequestBlockReactions(ChangeEvent changeEvent)
    {
        // Saved and restored rather than nulled, mirroring CurrentReactionTrigger. A block card
        // played below opens a nested window via its own introduction event; when that returns,
        // this window's remaining factions must get their trigger identity back — block trigger
        // conditions, the self-block guard and the prompt's trigger label all read it.
        ChangeEvent previousBlockTrigger = CurrentBlockTrigger;
        CurrentBlockTrigger = changeEvent;
        try
        {
            // Only the team that did not cause the event is offered a block. Every block card in the
            // game reacts to an opponent's unit removal or forced discard, so a turn for the acting
            // team would be an empty window in every case that actually exists.
            FactionTeam team = StaticGameData.OpponentFactionTeamForFaction(changeEvent.TriggeringFaction);
            HashSet<Faction> passed = new();

            while (!changeEvent.IsBlocked)
            {
                // Once per turn rather than once per faction — a turn raises all of its prompts from
                // one tag snapshot, so the tagsDirty bookkeeping this replaced is no longer needed and
                // this is strictly fewer CalculateAll calls. Inside the loop, not before it: block
                // trigger conditions read CurrentBlockTrigger (set above), and a block card resolved
                // below runs nested windows that recalculate under a different trigger.
                GameStateCalculator.CalculateAll();

                List<Faction> candidates = StaticGameData.FactionsForTeam(team)
                    .Where(f => f != changeEvent.TriggeringFaction)   // nobody blocks their own action
                    .Where(f => !passed.Contains(f))
                    .Where(f => ShouldOpenReactionWindow(f, GetBlockReactionOptions(f)))
                    .ToList();

                (Faction faction, int cardId) = await TakeTeamTurn(candidates, RequestBlock, passed);
                if (cardId == -1)
                    break; // The whole team declined — nothing further will block this event

                DebugUtilities.PrintPeer($"BLOCK REACTION from {FactionState.ForEnum(faction).FactionLabel}");
                await DoCard(cardId, changeEvent);
            }
        }
        finally
        {
            CurrentBlockTrigger = previousBlockTrigger;
        }
    }

    private async Task<bool> RequestAfterReactions(ChangeEvent triggerEvent)
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

        // Whose turn it is. Fixed once, from the event that opened THIS window, and then tracked here
        // rather than re-derived: the flat RequestOrder this replaced read LastChangeEvent, which every
        // nested reaction and every prerequisite Apply() moves out from under it.
        //
        // A LOCAL, deliberately, not a field. A reaction played below recurses into DoCard, which opens
        // its own windows with their own pointers; when that unwinds, this frame resumes on the team
        // whose turn it actually was. A field would be clobbered by the nested window.
        FactionTeam teamToAct = FirstTeamToReactTo(triggerEvent);

        try
        {
            // Two team turns in a row with nothing played closes the window.
            int consecutiveTeamPasses = 0;
            while (consecutiveTeamPasses < 2)
            {
                // Recalculate tags at the start of every turn so Immediate-scope
                // EventConditions always evaluate against the current (possibly restored)
                // CurrentReactionTrigger — including after a nested reaction card resolves
                // and the trigger is restored from the finally block.
                GameStateCalculator.CalculateAll();

                List<Faction> candidates = new();
                Dictionary<Faction, List<int>> offers = new();
                foreach (Faction faction in StaticGameData.FactionsForTeam(teamToAct))
                {
                    if (_afterReactionPassedFactions.Contains(faction)) continue;

                    List<int> options = GetAfterReactionOptions(faction);
                    // A faction the gate declines is re-evaluated on this team's next turn rather than
                    // recorded as having passed, exactly as a faction with no options always has been —
                    // the gate is a local state read, so there is no round-trip to save by caching it.
                    if (!ShouldOpenReactionWindow(faction, options)) continue;

                    candidates.Add(faction);
                    offers[faction] = options;
                }

                (Faction winner, int cardId) = await TakeTeamTurn(
                    candidates,
                    (faction, ct) => RequestPlay(faction, offers[faction], ct),
                    _afterReactionPassedFactions);

                if (cardId != -1)
                {
                    DebugUtilities.PrintPeer($"AFTER REACTION from {FactionState.ForEnum(winner).FactionLabel}");
                    _afterReactionPassedFactions.Clear(); // A reaction is about to happen — give everyone a fresh chance
                    await DoCard(cardId, triggerEvent);
                    anyEverPlayed = true;
                    consecutiveTeamPasses = 0;
                }
                else
                {
                    consecutiveTeamPasses++;
                }

                // Unconditionally, played or passed. This is the other half of the rule: a team that
                // has just used a reaction does not get to use a second one until the other side has
                // had a chance to react in between.
                teamToAct = StaticGameData.OpponentTeam(teamToAct);
            }
            DebugUtilities.PrintPeer("Both teams passed — no more after-reactions to this trigger");
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
    /// <param name="withdrawToken">
    /// Cancelled by <see cref="TakeTeamTurn"/> when another faction on this team has already used the
    /// team's one reaction for this turn. The prompt closes and this returns -1, the same as a pass —
    /// the caller distinguishes the two by the token, not by the return value.
    /// </param>
    public async Task<int> RequestPlay(Faction faction, List<int> reactionOptions = null,
                                       CancellationToken withdrawToken = default)
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
                    // Only on this branch. The else branch below is the faction's own play, which can
                    // carry a trigger too (the stamp under it is not gated on isReaction) — but it is
                    // not a reaction window, and labelling it "after reaction" would say it was.
                    TriggerReactionKind = TriggerKind.AFTER,
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
                // Not derivable from TriggerCardId above: that falls back to the last card in the pool
                // when the event has no source card, so it is what to SHOW, not what caused this.
                request.TriggerCauseText = CurrentReactionTrigger.CauseText();
                StampTriggerTargets(request, CurrentReactionTrigger);
            }

            InputRequest responseDto = await NetworkApi.Instance.SendInputRequest(request, withdrawToken);
            // Not for a withdrawn prompt: the player never got to answer it, so there is no skip scope
            // of theirs to record — reading the untouched default would silence them for the rest of
            // the turn step on a decision somebody else made.
            if (isReaction && !withdrawToken.IsCancellationRequested)
                GameFlow.Instance.RecordReactionSkip(faction, responseDto.ReactionSkipScope);
            selectedId = responseDto.ResponseCardIds.Count > 0 ? responseDto.ResponseCardIds[0] : -1;
        }


        return selectedId;
    }

    /// <summary>Ask the faction to choose a block-reaction step, or pass.</summary>
    /// <param name="withdrawToken">See <see cref="RequestPlay"/> — the team's turn was decided by one
    /// of its other factions and this prompt is being taken back off the screen.</param>
    public async Task<int> RequestBlock(Faction faction, CancellationToken withdrawToken = default)
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
            await ReplayContext.Pace(10);
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
            TriggerSummaryText = CurrentBlockTrigger?.SummaryText(),
            // See RequestPlay: the cause is the trigger's own source card, which TriggerCardId only
            // coincides with when the event has one. The kind is BLOCK from the handler's constructor.
            TriggerCauseText = CurrentBlockTrigger?.CauseText()
        };
        StampTriggerTargets(request, CurrentBlockTrigger);

        InputRequest responseDto = await NetworkApi.Instance.SendInputRequest(request, withdrawToken);
        // See RequestPlay: a withdrawn prompt carries no decision of this player's to record.
        if (!withdrawToken.IsCancellationRequested)
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
    /// A card cannot be its own window's cover, but that no longer needs handling here: introduction
    /// events open no reaction window at all, so the only window a freshly played Response card could
    /// have been asked about — its own — does not exist.
    /// </summary>
    public static bool HasHiddenResponseCards(Faction faction) =>
        DeckState.ForFaction(faction).ResponseCardIds
            .Any(id => CardState.ForId(id)?.IsRevealed == false);

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
    /// Every table card the faction can activate during its Play step instead of playing from hand —
    /// what the hand-play prompt draws beside the hand, whether or not each one can be used right now.
    /// The prompt greys out and un-clicks everything outside <see cref="ActivatableCardIds"/>.
    ///
    /// The same reasoning as <see cref="ReactionWindowDisplayCardIds"/>: a card the player knows they
    /// have on the table, simply missing from the prompt, reads as a bug — and the hand it sits beside
    /// already shows its unplayable cards greyed rather than hiding them.
    ///
    /// Scanned over CardState rather than the DeckState piles, the same way <see cref="ActivatableCardIds"/>
    /// next to it does — a Bulletin is in NO pile (see BulletinCardState), so a pile scan dropped the
    /// always-available Reallocate Resources action from the fan on exactly the turns it was not usable,
    /// which is the opposite of the greying-out this method exists for.
    ///
    /// IsPlayed is what keeps a Status card still in HAND out of the side fan: CardTriggers() does not
    /// depend on it, so an unplayed one would otherwise appear in the hand fan and the side fan at once.
    /// It is also what BulletinCardState hardcodes to true, so a Bulletin passes. IsDiscarded is the
    /// other half: a discarded card keeps Tag.IsPlayed (see GameStateCalculator.CalculatePlayedCardsForFaction),
    /// so a Status card that has left the table would come back as a side-fan card without it.
    /// </summary>
    public static List<int> PlayStepActivationCardIds(Faction faction)
    {
        return CardState.AllForFaction(faction).Values
            .Where(cardState => cardState.IsPlayed
                             && !cardState.IsDiscarded
                             && cardState.CardLogic?.IsPlayStepActivation == true)
            .Select(cardState => cardState.Id)
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
    /// </summary>
    private static bool ShouldOpenReactionWindow(Faction faction, List<int> options)
    {
        // A scoped skip only silences the windows that exist to hide information. A face-up Status
        // card (or an already-revealed Response) is public knowledge, so the player keeps that
        // window — only the ones that exist solely because of face-down cards go away.
        if (GameFlow.Instance.IsSkippingReactions(faction))
            return options.Any(IsPubliclyVisibleTableCard);

        return options.Count > 0 || HasHiddenResponseCards(faction);
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

    /// <summary>
    /// Split the trigger's board targets by kind for the wire, so the prompt can show the player WHERE
    /// the event they are reacting to landed and not just what caused it.
    ///
    /// TargetsOrNone, never Targets(): the removal events resolve a unit that the very next step may
    /// have taken off the board, and a read that threw here would cost a turn rather than a preview.
    /// Same rule <see cref="InputRequest.PopulateCardTargetPreviews"/> follows. A null trigger is fine
    /// — RequestBlock's CurrentBlockTrigger is nullable and TargetsOrNone answers None for it.
    /// </summary>
    private static void StampTriggerTargets(InputRequest request, ChangeEvent trigger)
    {
        TargetSet targets = trigger.TargetsOrNone();
        request.TriggerTargetCountryIds = targets.CountryIds.ToList();
        request.TriggerTargetUnitIds = targets.UnitIds.ToList();
    }
}
