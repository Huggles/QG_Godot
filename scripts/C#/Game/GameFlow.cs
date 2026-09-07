using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class GameFlow : SingletonNode<GameFlow>
{
    [Export] private bool GameStarted { get; set; } = false;
    [Export] public int GameTurn { get; set; } = 0;
    /// <summary>
    /// While true, assigning <see cref="TurnStepCounter"/> does NOT run the step handler.
    ///
    /// Exists for one caller: <see cref="ApplyFlowSnapshot"/>, which has to put the counter back where
    /// the save left it without re-executing the step it names. The resume that follows is what starts
    /// the loop again.
    /// </summary>
    private bool _suppressStepHandler = false;

    [Export] public int TurnStepCounter {
        get;
        set
        {
            field = value;
            DebugUtilities.PrintPeer($"TurnStepCounter: {field}");
            if(field > 0 && !_suppressStepHandler)
            {
                DispatchTurnStep(field);
            }
        } } = 0;

    /// <summary>
    /// Run the step the counter now names.
    ///
    /// Extracted from the setter above rather than left inline because a property setter cannot be
    /// overridden piecemeal, and <see cref="TurnStepCounter"/> must stay exactly as declared: it is an
    /// [Export] addressed by name from GameFlowMultiplayerSynchronizer's SceneReplicationConfig, so
    /// re-declaring it in a derived class — or making it virtual — puts the replication path in doubt.
    ///
    /// Only the dispatch is host-gated. The NextStepStarted emit below runs on every peer and is what
    /// drives the HUD; a client's GameFlow does nothing else with the counter.
    /// </summary>
    protected virtual void DispatchTurnStep(int counter)
    {
        GameTurnStep gameTurnStep = TurnSteps[counter - 1];
        if(Multiplayer.IsServer())
        {
            // Guard, not a bare call: Handler is a Func<Task> and the returned Task used to
            // be discarded, so any exception below it (card steps, ChangeEvent.Apply,
            // InputRequest, GameAPI) went into an unobserved Task and the loop simply stalled
            // forever with nothing logged. This is the single highest-leverage seam here.
            Guard.FireAndForget(gameTurnStep.Handler,
                $"TurnStep {gameTurnStep.TurnStep}", CurrentFaction, stallsLoop: true);
        }
        EventBus.Emit(EventBus.SignalName.NextStepStarted, (int)gameTurnStep.TurnStep);
    }
    [Export] public TurnStep TurnStep { get; set; } = 0;
    [Export] public int MaxRound { get; set; } = 20;

    /// <summary>
    /// Whether the game opens with the mandatory discard. True: deal OpeningHandSize and cut back to
    /// HandSize before the first turn. False: deal HandSize and start.
    ///
    /// Set from the scenario (with an optional lobby override) in
    /// GameModeMultiplayerDefault.SetupInitialGameState, which is awaited before StartGame. Host-only
    /// and deliberately not on the GameFlowMultiplayerSynchronizer — it gates work whose whole effect
    /// reaches clients as replicated ChangeEvents, exactly like MaxRound.
    /// </summary>
    public bool OpeningDiscardEnabled { get; set; } = true;

    public int Round => StaticGameData.RoundForTurn(GameTurn);
    public Faction CurrentFaction => GameTurn > 0 ? StaticGameData.PlayableFactions[(GameTurn - 1) % StaticGameData.PlayableFactions.Count] : Faction.GERMANY;
    private MultiplayerGameState gameState => GameSession.Current.GameState;
    public FactionState CurrentFactionState => FactionState.ForEnum(CurrentFaction);
    public DeckState CurrentFactionDeckState => DeckState.ForFaction(CurrentFaction);
    public Dictionary<Faction, int> CardsPlayedThisTurnStep = new Dictionary<Faction, int>();

    /// <summary>
    /// Reaction windows a faction has opted out of via the scoped skip buttons on a reaction prompt.
    /// Entries are self-expiring: each records the window it was set in and is simply no longer
    /// honoured once the game has moved past it, so there is no clearing hook to keep in step with
    /// the turn loop.
    ///
    /// Host-only, like <see cref="CardsPlayedThisTurnStep"/>: it decides whether the host opens a
    /// window at all, and the suppression reaches clients as the absence of an InputRequest. Not on
    /// the GameFlowMultiplayerSynchronizer and deliberately not in MultiplayerGameState.ComputeHash —
    /// a peer cannot get it wrong because a peer never has it.
    /// </summary>
    private readonly Dictionary<Faction, (int Turn, TurnStep Step)> reactionSkipTurnStep = new();
    private readonly Dictionary<Faction, int> reactionSkipRound = new();

    /// <summary>
    /// Record a faction's scoped skip choice from a reaction prompt. NONE is a no-op, and so is
    /// UNTIL_ACTIVATABLE - see the case below.
    /// </summary>
    public void RecordReactionSkip(Faction faction, ReactionSkipScope scope)
    {
        switch (scope)
        {
            case ReactionSkipScope.UNTIL_ACTIVATABLE:
                // Deliberately not recorded. That scope is enforced on the client, which answers its
                // own empty windows; honouring it here would stop the window opening at all, and its
                // appearance would then be proof that the faction holds a face-down Response card
                // matching exactly this event. The always-ask rule exists to deny that inference.
                break;

            case ReactionSkipScope.TURN_STEP:
                reactionSkipTurnStep[faction] = (GameTurn, TurnStep);
                DebugUtilities.PrintPeer($"{faction} is skipping reactions for the rest of turn {GameTurn} step {TurnStep}");
                break;

            case ReactionSkipScope.ROUND:
                reactionSkipRound[faction] = Round;
                DebugUtilities.PrintPeer($"{faction} is skipping reactions for the rest of round {Round}");
                break;
        }
    }

    /// <summary>
    /// Whether the faction has an active scoped skip. Callers must still offer the window when the
    /// faction holds a publicly visible reaction — see CardPlayRound.ShouldOpenReactionWindow. This
    /// answers only "did they ask to be left alone", not "may they be skipped".
    /// </summary>
    public bool IsSkippingReactions(Faction faction)
    {
        if (reactionSkipTurnStep.TryGetValue(faction, out (int Turn, TurnStep Step) window)
            && window.Turn == GameTurn && window.Step == TurnStep)
            return true;

        return reactionSkipRound.TryGetValue(faction, out int round) && round == Round;
    }

    /// <summary>
    /// Drop the incoming faction's round-scope skip. Reacting to your own battle (Destroyer
    /// Transport, Surprise Attack) is core play and must not be silently muted by a decision taken
    /// on somebody else's turn, so a round-scope skip never survives into its owner's own turn.
    /// Called from ChangeRoundChangeEvent once GameTurn — and therefore CurrentFaction — has moved.
    /// </summary>
    public void DropOwnTurnReactionSkip() => reactionSkipRound.Remove(CurrentFaction);

    public FactionTeam CurrentFactionTeam => (GameTurn > 0 && GameTurn % 2 == 0) ? FactionTeam.ALLIES : FactionTeam.AXIS;
    
    public Dictionary<Faction, List<VPTurnSummary>> VictoryPointSummaries = new Dictionary<Faction, List<VPTurnSummary>>();

    public List<CardPlayRound> CardPlayRounds { get; } = new();
    public CardPlayRound CurrentCardPlayRound => CardPlayRounds.LastOrDefault();

    private List<GameTurnStep> _turnSteps;

    /// <summary>
    /// The turn program's step table, indexed by <see cref="DispatchTurnStep"/> as [counter - 1].
    ///
    /// Built on first dispatch rather than in a constructor: <see cref="BuildTurnSteps"/> is virtual
    /// and consults the installed <see cref="Program"/>, and a virtual call from a constructor runs
    /// before the derived object — or the program — exists. Nothing reads this before the first
    /// dispatch (<see cref="ApplyFlowSnapshot"/>'s restore sets the counter with the handler
    /// suppressed), so the laziness is observationally identical to the eager build it replaces.
    /// </summary>
    protected List<GameTurnStep> TurnSteps => _turnSteps ??= BuildTurnSteps();

    /// <summary>
    /// The step table this session runs. A <see cref="ITurnProgram"/> may supply its own; returning
    /// null from the program — which is what every program that only brackets the normal loop does —
    /// keeps the seven steps below.
    /// </summary>
    protected virtual List<GameTurnStep> BuildTurnSteps()
        => Program?.BuildTurnSteps(this) ?? DefaultTurnSteps();

    protected List<GameTurnStep> DefaultTurnSteps() => new List<GameTurnStep> {
        new GameTurnStep(TurnStep.START, StartTurnStep),
        new GameTurnStep(TurnStep.PLAY_CARD, PlayCardStep),
        new GameTurnStep(TurnStep.SUPPLY, SupplyStep),
        new GameTurnStep(TurnStep.VICTORY_POINT, VictoryPointStep),
        new GameTurnStep(TurnStep.DISCARD, DiscardStep),
        new GameTurnStep(TurnStep.DRAW, DrawStep),
        new GameTurnStep(TurnStep.END, StartNewTurn)
    };

    public InputRequest CurrentInputRequest { get; set; }

    // ── The turn program ────────────────────────────────────────────────────────

    /// <summary>
    /// The scripted program driving this session, or null for a normal game — in which case every
    /// hook that consults it takes the branch it took before this existed, statement for statement.
    /// That is deliberate: "the normal game is unchanged" is then provable by running one, not argued.
    ///
    /// Host-only, and that is not a restriction. Every seam that consults it is already behind
    /// Multiplayer.IsServer(), and the one thing that uses it — tutorial mode — is single player, so
    /// the host is the only peer there is.
    /// </summary>
    public ITurnProgram Program { get; private set; }

    /// <summary>
    /// Install a turn program. Must happen before <see cref="StartGame"/>: the step table is built on
    /// first dispatch and cached, so a program installed after the loop is running would be ignored
    /// by <see cref="TurnSteps"/> and honoured by the hooks — the worst of both.
    /// </summary>
    public void InstallProgram(ITurnProgram program)
    {
        if (!Multiplayer.IsServer())
        {
            DebugUtilities.PrintPeerError("InstallProgram: the turn program is host-only");
            return;
        }
        if (GameStarted)
        {
            DebugUtilities.PrintPeerError("InstallProgram: too late, the game has already started");
            return;
        }

        Program = program;
        program?.Attach(this);
    }

    // No handler for TurnStep.START: it raises no prompt and finishes inline — see StartTurnStepBody.
    public PlayStepHandlerDefault playStepHandlerDefault;
    public IVictoryStepHandler vpStepHandler = new VictoryStepHandlerDefault();
    public IDiscardStepHandler discardStepHandler;
    public IDrawStepHandler drawStepHandler;
    public ISupplyStepHandler supplyStepHandler;

    public async void StartGame()
    {
        // async void: an uncaught throw here would kill the process rather than surface.
        try
        {
            DebugUtilities.PrintPeer("GameFlow: Starting game");
            GameStateCalculator.Enabled = false;
            // Draw *to* the opening hand size, not a flat deal. A scenario's initialHandCards are
            // already in hand by now (SetupInitialGameState is awaited before StartGame in
            // MultiplayerSession), so a flat draw dealt OpeningHandSize + N and the faction had to dump
            // the surplus at its first DISCARD step. Same shape as DrawStepHandlerDefault, which is the
            // draw-to-HandSize every later turn uses.
            //
            // Host-only by construction (StartGame runs behind the IsServer guard), so the count is
            // computed once against the authoritative hand and the resulting draws reach clients over
            // the replicated ChangeEvent stream.
            //
            // The two halves of the opening-discard rule move together: deal the larger hand ONLY if
            // the cut is coming. Dealing OpeningHandSize with the discard switched off would open
            // every faction over the hand cap and dump the surplus at their first DISCARD step.
            int openingHandSize = OpeningDiscardEnabled ? StaticGameData.OpeningHandSize : StaticGameData.HandSize;
            foreach (FactionState faction in gameState.PlayableFactionStates)
            {
                int cardsToDraw = openingHandSize - DeckState.ForFaction(faction.Faction).HandCardIds.Count;
                if (cardsToDraw <= 0) continue; // scenario already placed a full hand or more

                DrawCardsChangeEvent drawCardsChangeEvent =  new DrawCardsChangeEvent(Faction.NONE, faction.Faction, cardsToDraw, false);
                drawCardsChangeEvent.IsTrigger = false;
                await CardPlayPool.DoChangeEvent(drawCardsChangeEvent);
            }
            GameStateCalculator.Enabled = true;
            GameStateCalculator.CalculateAll();

            // Everyone cuts down to HandSize before the first turn starts. After the recalculation
            // above so the discard modal shows correctly-tagged cards, and before StartNewTurn so it
            // lands ahead of Germany's TurnStep.START rather than inside it.
            if (OpeningDiscardEnabled)
                await OpeningDiscard.Run(StaticGameData.OpeningDiscardCount);

            foreach (Faction faction in StaticGameData.PlayableFactions)
            {
                VictoryPointSummaries[faction] = new List<VPTurnSummary>();
            }

            GameStarted = true;

            // Before the first turn rather than after it: a program that opens with a message wants
            // to say it over the dealt board, not over Germany's already-running START step.
            if (Program != null) await Program.OnGameStarted();

            Guard.FireAndForget(StartNewTurn, "GameFlow.StartNewTurn", stallsLoop: true);
        }
        catch (Exception e)
        {
            ErrorReporter.Report(e, "GameFlow.StartGame");
        }
    }

    private async Task StartNewTurn()
    {
        DebugUtilities.PrintPeer("StartNewTurn");
        ErrorInjection.MaybeThrow(ErrorInjection.Site.EndStep);
        ErrorInjection.MaybeReportRecovered(ErrorInjection.Site.SoftRecovered);

        // Game-end check: only after the United States (the last faction of each round)
        // has just finished its turn. At this point GameTurn still holds the just-finished
        // turn, so CurrentFaction / Round describe the turn that ended.
        if (CurrentFaction == Faction.UNITED_STATES)
        {
            int axis   = StaticGameData.ScoreForTeam(FactionTeam.AXIS);
            int allies = StaticGameData.ScoreForTeam(FactionTeam.ALLIES);
            bool pointLead  = Math.Abs(axis - allies) >= 30;
            bool roundLimit = Round >= MaxRound;
            if (pointLead || roundLimit)
            {
                GameResult result = BuildGameResult(axis, allies, pointLead ? "30-point lead" : $"Round {MaxRound} reached");
                DebugUtilities.PrintPeer($"Game over ({result.EndReason}) — Axis {axis} / Allies {allies}, winner {result.WinningTeam}");
                MultiplayerSession.Instance.Rpc(nameof(MultiplayerSession.BeginEndGame), JsonSerializer.Serialize(result));
                return; // stop the loop: do NOT start a new turn / round
            }
        }

        await new ChangeRoundChangeEvent(GameTurn + 1).Apply();
        TurnStepCounter = 0;
        DebugUtilities.PrintPeer($"Game turn: {GameTurn} ( {Enum.GetName(typeof(Faction), CurrentFaction)} / {Enum.GetName(typeof(FactionTeam), CurrentFactionTeam)} )");
        EventBus.Emit(EventBus.SignalName.NewTurnStarted, GameTurn);
        if (Program != null) await Program.OnTurnStarted(GameTurn, CurrentFaction);
        await ReplayContext.Pace(100);
        StartNextStep();
    }

    /// <summary>
    /// Builds the end-of-game summary from the current (final) game state. Team totals come
    /// from FactionState.Score; the per-faction, per-round breakdown is aggregated from
    /// VictoryPointSummaries (which may hold several summaries per round when cards score
    /// points on top of the VICTORY_POINT step). Axis wins on an exact tie.
    /// </summary>
    private GameResult BuildGameResult(int axisTotal, int alliesTotal, string reason)
    {
        GameResult result = new GameResult
        {
            WinningTeam = alliesTotal > axisTotal ? FactionTeam.ALLIES : FactionTeam.AXIS,
            IsDraw = false,
            AxisTotal = axisTotal,
            AlliesTotal = alliesTotal,
            FinalRound = Round,
            EndReason = reason
        };

        foreach (Faction faction in StaticGameData.PlayableFactions)
        {
            FactionState factionState = FactionState.ForEnum(faction);
            List<VPTurnSummary> summaries = VictoryPointSummaries.GetValueOrDefault(faction, new List<VPTurnSummary>());

            List<RoundScore> perRound = summaries
                .GroupBy(s => s.Round)
                .OrderBy(g => g.Key)
                .Select(g => new RoundScore { Round = g.Key, Points = g.Sum(s => s.TotalScore) })
                .ToList();

            result.Factions.Add(new FactionResult
            {
                Faction = faction,
                Team = StaticGameData.FactionTeamForFaction(faction),
                Total = factionState?.Score ?? 0,
                PerRound = perRound,
                FactionData = StaticGameData.FactionDataMap.GetValueOrDefault(faction)
            });
        }

        return result;
    }

    /// <summary>
    /// Epoch the current step belongs to. Any advance requested by a handler from an earlier epoch
    /// is an out-of-band call from an aborted step and is ignored — see <see cref="StartNextStep"/>.
    /// </summary>
    private int stepEpoch = 0;

    public void StartNextStep()
    {
        // Gate on the epoch. The play-step handlers subscribe to the GLOBAL
        // EventBus.CardPlayPoolFinished signal and only unsubscribe inside their own OnRoundFinished,
        // so an aborted step leaves a live subscription behind. Without this gate, the next round to
        // finish — including the START step's — would fire the zombie handler and advance the loop a
        // second time, silently skipping a step.
        if (stepEpoch != ErrorReporter.GameLoopEpoch)
        {
            DebugUtilities.PrintPeer(
                $"Ignoring StartNextStep from stale epoch {stepEpoch} (current {ErrorReporter.GameLoopEpoch})");
            return;
        }

        DebugUtilities.PrintPeer("StartNextStep");
        TurnStepCounter++;
    }

    /// <summary>
    /// Open a turn step: apply its ChangeStepChangeEvent (which sets TurnStep and starts a fresh
    /// CardPlayRound), then run any BEFORE mutators. Ordering matters — the mutators need the new
    /// round to exist so their change events can go through the reaction pipeline.
    /// </summary>
    private async Task BeginStep(TurnStep step)
    {
        await new ChangeStepChangeEvent(step).Apply();

        // Ahead of the BEFORE mutators, not behind them: a program's narration introduces the step,
        // so it must land before any mutator announces its own Bulletin over the top of it.
        if (Program != null) await Program.BeforeStep(step, CurrentFaction);

        await StepMutatorRunner.Run(step, MutatorTiming.BEFORE, CurrentFaction);
    }

    /// <summary>
    /// A step has completed: run its AFTER mutators, then advance.
    ///
    /// Separate from <see cref="StartNextStep"/> for two reasons. The completed step is only known at
    /// the callback, since five of the six steps finish through an event rather than an awaited Task.
    /// And StartNewTurn also calls StartNextStep for the turn rollover — routing that through here
    /// would re-run the DRAW step's AFTER mutators, because TurnStep still reads DRAW at that point
    /// (TurnStep.END emits no ChangeStepChangeEvent).
    /// </summary>
    private void FinishStep(TurnStep completed)
    {
        // Same stale-epoch gate as StartNextStep: an aborted step can leave a live subscription
        // behind, and we must not run its mutators against the resumed loop.
        if (stepEpoch != ErrorReporter.GameLoopEpoch)
        {
            DebugUtilities.PrintPeer(
                $"Ignoring FinishStep({completed}) from stale epoch {stepEpoch} (current {ErrorReporter.GameLoopEpoch})");
            return;
        }

        Guard.FireAndForget(async () =>
        {
            await StepMutatorRunner.Run(completed, MutatorTiming.AFTER, CurrentFaction);

            // The one moment in the loop that is reliably quiescent: the step is fully resolved, its
            // round has been finished and nulled, and the next one has not opened. A save the player
            // asked for mid-card lands here.
            TryFlushDeferredSave();

            // After the flush, so the checkpoint above stays the quiescent moment it documents, and
            // before the advance, so narration lands inside the step it is describing.
            if (Program != null) await Program.AfterStep(completed, CurrentFaction);

            StartNextStep();
        }, $"AfterMutators {completed}", CurrentFaction, stallsLoop: true);
    }

    /// <summary>
    /// Resume the turn loop after a reported failure, abandoning the rest of the failed step.
    /// Called from the error popup's Continue button via <c>ErrorReporter.RequestResume</c>, after
    /// the awaiter-cancel sweep has run and the epoch has been bumped.
    /// </summary>
    public void ResumeAfterFailure()
    {
        if (!Multiplayer.IsServer()) return;

        stepEpoch = ErrorReporter.GameLoopEpoch;
        DebugUtilities.PrintPeer($"ResumeAfterFailure at TurnStepCounter {TurnStepCounter}");

        // Guard.Try, not a bare call: a program throwing while it stands down must not take the
        // recovery path with it — the whole point here is to get the turn loop running again.
        Guard.Try(() => Program?.OnResumeAfterFailure(), "GameFlow.Program.OnResumeAfterFailure");

        // Tags are not part of the replicated hash, so a plain recalculation is enough to make the
        // resumed step's condition checks see current state.
        GameStateCalculator.CalculateAll();

        // The branch is required, not defensive. The table has 7 entries and the dispatch indexes
        // [counter - 1], so a failure during TurnStep.END (counter 7) would make TurnStepCounter++
        // index [7] and throw ArgumentOutOfRangeException from inside the recovery path itself.
        //
        // Re-running the LAST entry's handler rather than naming StartNewTurn: those are the same
        // thing for the default table, and stay the same thing for a program that supplies its own.
        if (TurnStepCounter >= TurnSteps.Count)
        {
            GameTurnStep last = TurnSteps[TurnSteps.Count - 1];
            Guard.FireAndForget(last.Handler, $"resume:{last.TurnStep}", stallsLoop: true);
        }
        else
            TurnStepCounter++;
    }

    /// <summary>
    /// Detach the in-flight step handler so its Finished event — which never fired, because the step
    /// threw — cannot fire later against the resumed loop, and so its global EventBus subscription
    /// does not leak.
    /// </summary>
    public void CancelCurrentStepHandler()
    {
        // Nothing for TurnStep.START: it has no handler and no round to leave in flight.
        playStepHandlerDefault?.Cancel();

        // Detach GameFlow's own subscriptions too: the handler instances stay referenced by these
        // fields, so a late Finished emission would otherwise still reach us.
        if (playStepHandlerDefault != null)
            playStepHandlerDefault.PlayStepFinished -= PlayCardStepFinishedHandler;
        if (supplyStepHandler != null)
            supplyStepHandler.SupplyStepFinished -= SupplyStepFinishedHandler;
        if (discardStepHandler != null)
            discardStepHandler.DiscardStepFinished -= DiscardStepFinishedHandler;
        if (drawStepHandler != null)
            drawStepHandler.DrawStepFinished -= DrawStepFinishedHandler;

        playStepHandlerDefault = null;
        supplyStepHandler = null;
        discardStepHandler = null;
        drawStepHandler = null;
    }

    /// <summary>Drop the shared input-request slot so a stale response cannot be mistaken for a live one.</summary>
    public void ClearCurrentInputRequest() => CurrentInputRequest = null;

    

    // ── Save / load ─────────────────────────────────────────────────────────────

    /// <summary>
    /// How <see cref="ResumeAfterLoad"/> should pick the loop back up. Set by
    /// <see cref="ApplyFlowSnapshot"/> from the save.
    /// </summary>
    private ResumeMode resumeMode = ResumeMode.NextStep;

    /// <summary>
    /// A save the player asked for at a moment the game could not honour, to be written at the next
    /// quiescent boundary. Null when nothing is pending.
    ///
    /// The alternative — greying the button out mid-card and mid-reaction — is a wall in front of a
    /// large share of the moments a player actually reaches for the menu, because the always-ask
    /// reaction rule means they are very often sitting on a reaction prompt.
    /// </summary>
    private string deferredSaveName;

    public bool HasDeferredSave => deferredSaveName != null;

    /// <summary>
    /// Fully quiescent: no card is resolving, nobody has been asked anything, and both queues are
    /// drained. <see cref="ResumeMode.NextStep"/> — the restored game advances to the step after the
    /// one recorded.
    ///
    /// <c>CardPlayRound.Current</c>, not <see cref="CurrentCardPlayRound"/>: the latter is the last
    /// entry of the CardPlayRounds history, which stays non-null after its round has finished. The
    /// static is nulled by CardPlayRound.Finish, so it is the one that answers "is a round in flight".
    /// </summary>
    public bool IsSafeToSave =>
        CardPlayRound.Current == null
        && QueuesIdle
        && !HasPendingInput;

    /// <summary>
    /// The current step has opened but nothing has happened in it yet: its round exists, no card has
    /// been played into it, no reaction is open. The board is therefore still exactly at step-start, so
    /// the step.s BODY can simply be re-run — and re-running it re-issues whatever it asks.
    /// <see cref="ResumeMode.ReRunCurrentStep"/>.
    ///
    /// Whether a prompt happens to be open right now is immaterial, which is why this does not test for
    /// one: the step is equally re-runnable in the beat between BeginStep and its handler asking.
    ///
    /// The two clauses are what make it safe. An empty ChangeEventsPool rules out a prompt raised from
    /// inside a card.s execution — every Apply registers into the pool, IsTrigger only suppresses the
    /// reaction chain — and a zero ReactionDepth rules out a reaction window. Neither of those can be
    /// resumed: the await holding them is a compiler-generated state machine partway through a lambda.
    /// </summary>
    public bool IsAtStepStart =>
        CardPlayRound.Current != null
        && CardPlayRound.Current.ChangeEventsPool.Count == 0
        && CardPlayRound.Current.ReactionDepth == 0
        && QueuesIdle;

    /// <summary>
    /// Whether the game can be captured right now.
    ///
    /// The ModifierRegistry clause is a backstop rather than a live concern — see
    /// <see cref="IUnsavedModifier"/>. Both checkpoints above already fall outside the window in which
    /// one can exist, so this only fires if a future card registers a step-local modifier somewhere new.
    /// Better a refused save than a silently missing effect on reload.
    /// </summary>
    public bool CanSave =>
        GameStarted
        && Program?.AllowsSaving != false
        && (IsSafeToSave || IsAtStepStart)
        && !ModifierRegistry.HasUnsavedModifiers;

    /// <summary>
    /// Why <see cref="CanSave"/> is false, or null when it is true. Exists so a refusal is actionable —
    /// "not at a point that can be resumed" tells a player nothing about what to do next.
    /// </summary>
    public string SaveBlockedReason
    {
        get
        {
            if (!GameStarted)                        return "the game has not started yet";
            // Above the rest: a program that refuses saving refuses at every moment, so the honest
            // answer is its reason and not whichever transient one happens to apply right now.
            if (Program?.AllowsSaving == false)      return Program.SaveBlockedReason;
            if (ModifierRegistry.HasUnsavedModifiers) return "a card effect is still waiting for the end of this step";
            if (IsSafeToSave || IsAtStepStart) return null;

            if (!QueuesIdle)                         return "the board is still resolving the last action";
            if (CardPlayRound.Current == null)       return "an action is in flight";
            if (CardPlayRound.Current.ReactionDepth > 0)
                                                     return "a card is resolving and reactions are open";
            if (CardPlayRound.Current.ChangeEventsPool.Count > 0)
                                                     return "a card has already been played this step";
            return "the game is mid-action";
        }
    }

    private static bool QueuesIdle =>
        (!IsInstanceValid(ChangeEventQueue.Instance) || ChangeEventQueue.Instance.IsIdle)
        && (!IsInstanceValid(AnimationQueue.Instance) || AnimationQueue.Instance.IsIdle);

    private static bool HasPendingInput => NetworkApi.Instance?.HasPendingInput == true;

    /// <summary>
    /// The counter to persist, so that a single resume mechanism — advance one step — lands correctly
    /// for both checkpoints.
    ///
    /// At a quiescent boundary the recorded step has completed, so the resume runs the NEXT one. At an
    /// initial-decision checkpoint the current step has not really started, so the previous counter is
    /// stored and the resume re-runs THIS step, re-issuing its prompt. This one line is the whole
    /// reason two resume modes do not need two mechanisms.
    /// </summary>
    private int SaveStepCounter => IsAtStepStart ? TurnStepCounter - 1 : TurnStepCounter;

    /// <summary>Turn-engine state a save needs and the ChangeEvent log does not carry.</summary>
    public GameFlowSnapshot BuildFlowSnapshot()
    {
        GameFlowSnapshot snapshot = new()
        {
            GameStarted             = GameStarted,
            GameTurn                = GameTurn,
            TurnStepCounter         = SaveStepCounter,
            TurnStep                = TurnStep,
            MaxRound                = MaxRound,
            CardsPlayedThisTurnStep = new Dictionary<Faction, int>(CardsPlayedThisTurnStep),
            VictoryPointSummaries   = new Dictionary<Faction, List<VPTurnSummary>>(VictoryPointSummaries),
            ReactionSkipRound       = new Dictionary<Faction, int>(reactionSkipRound)
        };

        foreach (KeyValuePair<Faction, (int Turn, TurnStep Step)> entry in reactionSkipTurnStep)
            snapshot.ReactionSkipTurnStep[entry.Key] =
                new ReactionSkipWindow { Turn = entry.Value.Turn, Step = entry.Value.Step };

        return snapshot;
    }

    /// <summary>
    /// Put the turn engine back where the save left it. The counter is restored WITHOUT firing its step
    /// handler; <see cref="ResumeAfterLoad"/> is what starts the loop again.
    /// </summary>
    public void ApplyFlowSnapshot(GameFlowSnapshot snapshot, ResumeMode resume)
    {
        resumeMode  = resume;
        GameStarted = snapshot.GameStarted;
        GameTurn    = snapshot.GameTurn;
        TurnStep    = snapshot.TurnStep;
        MaxRound    = snapshot.MaxRound;

        _suppressStepHandler = true;
        try { TurnStepCounter = snapshot.TurnStepCounter; }
        finally { _suppressStepHandler = false; }

        CardsPlayedThisTurnStep = new Dictionary<Faction, int>(snapshot.CardsPlayedThisTurnStep);

        VictoryPointSummaries = new Dictionary<Faction, List<VPTurnSummary>>();
        foreach (KeyValuePair<Faction, List<VPTurnSummary>> entry in snapshot.VictoryPointSummaries)
            VictoryPointSummaries[entry.Key] = new List<VPTurnSummary>(entry.Value);

        reactionSkipTurnStep.Clear();
        foreach (KeyValuePair<Faction, ReactionSkipWindow> entry in snapshot.ReactionSkipTurnStep)
            reactionSkipTurnStep[entry.Key] = (entry.Value.Turn, entry.Value.Step);

        reactionSkipRound.Clear();
        foreach (KeyValuePair<Faction, int> entry in snapshot.ReactionSkipRound)
            reactionSkipRound[entry.Key] = entry.Value;
    }

    /// <summary>
    /// Start the turn loop again after a restore. Host only, and called once the whole event log has
    /// been replayed and the HUD is up.
    /// </summary>
    public void ResumeAfterLoad()
    {
        if (!Multiplayer.IsServer()) return;

        // Adopt the current epoch, exactly as ResumeAfterFailure does: the loop we are about to start
        // must not be mistaken for a stale one by StartNextStep's and FinishStep's gates.
        stepEpoch = ErrorReporter.GameLoopEpoch;

        DebugUtilities.PrintPeer(
            $"ResumeAfterLoad: turn {GameTurn}, step {TurnStep}, counter {TurnStepCounter}, mode {resumeMode}");

        if (resumeMode == ResumeMode.ReRunCurrentStep)
            Guard.FireAndForget(() => RestartStepBody(TurnStep), $"resume:{TurnStep}", CurrentFaction, stallsLoop: true);
        else
            StartNextStep();
    }

    /// <summary>
    /// Check that the first prompt raised after a restore is the one the save was taken on.
    ///
    /// A warning rather than a failure: the game is playable either way, and the honest thing is to say
    /// the resume landed somewhere unexpected rather than to pretend or to refuse. Called from
    /// NetworkApi.SendInputRequest for the first request after a load.
    /// </summary>
    public void VerifyResumedPrompt(PendingPrompt expected, InputRequest actual)
    {
        if (expected == null) return;

        if (expected.Matches(actual))
            DebugUtilities.PrintPeer($"Restore resumed on the saved prompt ({expected}).");
        else
            DebugUtilities.PrintPeerErrorRaw(
                $"Restore resumed on {PendingPrompt.KindOf(actual)} for {actual.TargetFaction}, " +
                $"but the save was taken on {expected}. The board is restored; the turn position may not be.");
    }

    /// <summary>
    /// Ask for a save to be written at the next quiescent boundary. Used when the player presses Save
    /// during a card or a reaction window, where a capture would have nothing resumable to point at.
    /// </summary>
    public void RequestDeferredSave(string displayName)
    {
        // A program that refuses saving refuses deferred ones too. Without this the request would be
        // accepted and then silently never honoured — TryFlushDeferredSave short-circuits on CanSave,
        // so the name would simply sit here for the rest of the game.
        if (Program?.AllowsSaving == false)
        {
            DebugUtilities.PrintPeer($"Deferred save refused: {SaveBlockedReason}");
            return;
        }

        deferredSaveName = displayName;
        DebugUtilities.PrintPeer($"Save deferred to the next safe point: '{displayName}'");
    }

    public void CancelDeferredSave() => deferredSaveName = null;

    /// <summary>
    /// Write a deferred save if one is waiting and the game has reached a point that can carry it.
    /// Called from <see cref="FinishStep"/>, which is the moment a step's effects are fully resolved.
    /// </summary>
    private void TryFlushDeferredSave()
    {
        if (deferredSaveName == null || !CanSave) return;

        string displayName = deferredSaveName;
        deferredSaveName = null;
        MultiplayerSession.Instance?.CaptureSave(displayName, deferred: true);
    }

    // ── Turn steps ──────────────────────────────────────────────────────────────
    //
    // Each step is split into the step method, which opens the step and then runs its body, and the
    // body itself. The split exists for save/load: restoring a game that was saved on a step's opening
    // prompt has to re-run the body WITHOUT re-running BeginStep, which would apply a second
    // ChangeStepChangeEvent, start a second CardPlayRound, and — the one that actually breaks things —
    // run the step's BEFORE mutators a second time. See RestartStepBody.

    /// <summary>
    /// Re-run a step's body without re-opening the step. Host-only, and only from
    /// <see cref="ResumeAfterLoad"/>: everything the body needs (the round, the step, the BEFORE
    /// mutators) is already in place from the replayed log, so all that is missing is the handler that
    /// asks the player something.
    /// </summary>
    private async Task RestartStepBody(TurnStep step)
    {
        DebugUtilities.PrintPeer($"RestartStepBody({step}) — resuming the step the save was taken in");
        switch (step)
        {
            case TurnStep.START:         StartTurnStepBody(); break;
            case TurnStep.PLAY_CARD:     PlayCardStepBody(); break;
            case TurnStep.SUPPLY:        SupplyStepBody(); break;
            case TurnStep.VICTORY_POINT: await VictoryPointStepBody(); break;
            case TurnStep.DISCARD:       DiscardStepBody(); break;
            case TurnStep.DRAW:          DrawStepBody(); break;
            default:
                // TurnStep.END is StartNewTurn, which opens no step and raises no prompt, so it can
                // never be what a save was taken in. Advance rather than stall the loop.
                DebugUtilities.PrintPeerError($"RestartStepBody: {step} has no resumable body; advancing instead");
                StartNextStep();
                break;
        }
    }

    private async Task StartTurnStep()
    {
        await BeginStep(TurnStep.START);
        StartTurnStepBody();
    }

    /// <summary>
    /// The START step raises no prompt. It used to run a CardPlayRound of its own for the four cards
    /// that read "at the beginning of your turn", and that round only opened a prompt when one of them
    /// was activatable — so the prompt's existence told every opponent the faction held one, which for
    /// a face-down Response card is the whole secret. Those cards carry Condition.IsPlayCardStep now
    /// and are offered in the play prompt, which opens every turn regardless.
    ///
    /// The step itself stays: BeginStep still applies its ChangeStepChangeEvent (which starts the
    /// round the PLAY_CARD step's ChangeStepChangeEvent will replace) and still runs its BEFORE
    /// mutators, and FinishStep still runs its AFTER mutators. Kept as a body rather than folded into
    /// StartTurnStep so RestartStepBody has something to resume a save taken in this step with.
    /// </summary>
    private void StartTurnStepBody()
    {
        DebugUtilities.PrintPeer("StartTurnStep (no card round)");
        FinishStep(TurnStep.START);
    }

    private async Task PlayCardStep()
    {
        await BeginStep(TurnStep.PLAY_CARD);
        PlayCardStepBody();
    }

    private void PlayCardStepBody()
    {
        DebugUtilities.PrintPeer("PlayCardStep");
        playStepHandlerDefault = new PlayStepHandlerDefault();
        playStepHandlerDefault.PlayStepFinished += PlayCardStepFinishedHandler;
        playStepHandlerDefault.Start(CurrentFaction);
    }

    public void PlayCardStepFinishedHandler()
    {
        playStepHandlerDefault.PlayStepFinished -= PlayCardStepFinishedHandler;
        FinishStep(TurnStep.PLAY_CARD);
    }

    private async Task SupplyStep()
    {
        await BeginStep(TurnStep.SUPPLY);
        SupplyStepBody();
    }

    private void SupplyStepBody()
    {
        DebugUtilities.PrintPeer("SupplyStep");
        supplyStepHandler = new SupplyStepHandlerDefault();
        supplyStepHandler.SupplyStepFinished += SupplyStepFinishedHandler;
        supplyStepHandler.Start(CurrentFaction);
    }

    public void SupplyStepFinishedHandler()
    {
        supplyStepHandler.SupplyStepFinished -= SupplyStepFinishedHandler;
        FinishStep(TurnStep.SUPPLY);
    }

    private async Task VictoryPointStep()
    {
        await BeginStep(TurnStep.VICTORY_POINT);
        await VictoryPointStepBody();
    }

    private async Task VictoryPointStepBody()
    {
        DebugUtilities.PrintPeer("VictoryPointStep");
        await vpStepHandler.ProcessVictoryStep(CurrentFaction);
        FinishStep(TurnStep.VICTORY_POINT);
    }

    private async Task DiscardStep()
    {
        await BeginStep(TurnStep.DISCARD);
        DiscardStepBody();
    }

    private void DiscardStepBody()
    {
        DebugUtilities.PrintPeer("DiscardStep");
        discardStepHandler = new DiscardStepHandlerDefault();
        discardStepHandler.DiscardStepFinished += DiscardStepFinishedHandler;
        discardStepHandler.Start(CurrentFaction);
    }

    public void DiscardStepFinishedHandler()
    {
        discardStepHandler.DiscardStepFinished -= DiscardStepFinishedHandler;
        FinishStep(TurnStep.DISCARD);
    }

    private async Task DrawStep()
    {
        await BeginStep(TurnStep.DRAW);
        DrawStepBody();
    }

    private void DrawStepBody()
    {
        DebugUtilities.PrintPeer("DrawStep");
        drawStepHandler = new DrawStepHandlerDefault();
        drawStepHandler.DrawStepFinished += DrawStepFinishedHandler;
        drawStepHandler.Start(CurrentFaction);
    }

    public void DrawStepFinishedHandler()
    {
        drawStepHandler.DrawStepFinished -= DrawStepFinishedHandler;
        FinishStep(TurnStep.DRAW);
    }

    public class GameTurnStep
    {
        public TurnStep TurnStep;
        public Func<Task> Handler;        

        public GameTurnStep(TurnStep turnStep, Func<Task> handler)
        {
            TurnStep = turnStep;
            Handler = handler;
        }
    }
}
