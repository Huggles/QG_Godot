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
    [Export] public int TurnStepCounter { 
        get; 
        set
        {            
            field = value;
            DebugUtilities.PrintPeer($"TurnStepCounter: {field}");
            if(field > 0)
            {
                GameTurnStep gameTurnStep = gameTurnSteps[field - 1];
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
        } } = 0;
    [Export] public TurnStep TurnStep { get; set; } = 0;
    [Export] public int MaxRound { get; set; } = 20;

    public int Round => StaticGameData.RoundForTurn(GameTurn);
    public Faction CurrentFaction => GameTurn > 0 ? StaticGameData.PlayableFactions[(GameTurn - 1) % StaticGameData.PlayableFactions.Count] : Faction.GERMANY;
    private MultiplayerGameState gameState => GameSession.Current.GameState;
    public FactionState CurrentFactionState => FactionState.ForEnum(CurrentFaction);
    public DeckState CurrentFactionDeckState => DeckState.ForFaction(CurrentFaction);
    public Dictionary<Faction, int> CardsPlayedThisTurnStep = new Dictionary<Faction, int>();

    public FactionTeam CurrentFactionTeam => (GameTurn > 0 && GameTurn % 2 == 0) ? FactionTeam.ALLIES : FactionTeam.AXIS;
    
    public Dictionary<Faction, List<VPTurnSummary>> VictoryPointSummaries = new Dictionary<Faction, List<VPTurnSummary>>();

    public List<CardPlayRound> CardPlayRounds { get; } = new();
    public CardPlayRound CurrentCardPlayRound => CardPlayRounds.LastOrDefault();

    private List<GameTurnStep> gameTurnSteps;

    public InputRequest CurrentInputRequest { get; set; }
    
    public GameFlow()
    {
        gameTurnSteps = new List<GameTurnStep> {
            new GameTurnStep(TurnStep.START, StartTurnStep),
            new GameTurnStep(TurnStep.PLAY_CARD, PlayCardStep),
            new GameTurnStep(TurnStep.SUPPLY, SupplyStep),
            new GameTurnStep(TurnStep.VICTORY_POINT, VictoryPointStep),
            new GameTurnStep(TurnStep.DISCARD, DiscardStep),
            new GameTurnStep(TurnStep.DRAW, DrawStep),
            new GameTurnStep(TurnStep.END, StartNewTurn)
        };
    }

    public StartTurnStepHandler startTurnStepHandler;
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
            // Draw *to* seven, not a flat seven. A scenario's initialHandCards are already in hand by
            // now (SetupInitialGameState is awaited before StartGame in MultiplayerSession), so a flat
            // draw dealt 7 + N and the faction had to dump the surplus at its first DISCARD step.
            // Same shape as DrawStepHandlerDefault, which is the draw-to-7 every later turn uses.
            //
            // Host-only by construction (StartGame runs behind the IsServer guard), so the count is
            // computed once against the authoritative hand and the resulting draws reach clients over
            // the replicated ChangeEvent stream.
            foreach (FactionState faction in gameState.PlayableFactionStates)
            {
                int cardsToDraw = 7 - DeckState.ForFaction(faction.Faction).HandCardIds.Count;
                if (cardsToDraw <= 0) continue; // scenario already placed a full hand or more

                DrawCardsChangeEvent drawCardsChangeEvent =  new DrawCardsChangeEvent(Faction.NONE, faction.Faction, cardsToDraw, false);
                drawCardsChangeEvent.IsTrigger = false;
                await CardPlayPool.DoChangeEvent(drawCardsChangeEvent);
            }
            GameStateCalculator.Enabled = true;
            GameStateCalculator.CalculateAll();

            foreach (Faction faction in StaticGameData.PlayableFactions)
            {
                VictoryPointSummaries[faction] = new List<VPTurnSummary>();
            }

            GameStarted = true;


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
            int axis   = StaticGameData.FactionsForTeam(FactionTeam.AXIS).Sum(f => FactionState.ForEnum(f).Score);
            int allies = StaticGameData.FactionsForTeam(FactionTeam.ALLIES).Sum(f => FactionState.ForEnum(f).Score);
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
        await Task.Delay(100);
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

        // Tags are not part of the replicated hash, so a plain recalculation is enough to make the
        // resumed step's condition checks see current state.
        GameStateCalculator.CalculateAll();

        // The branch is required, not defensive. gameTurnSteps has 7 entries and the setter indexes
        // [counter - 1], so a failure during TurnStep.END (counter 7) would make TurnStepCounter++
        // index [7] and throw ArgumentOutOfRangeException from inside the recovery path itself.
        if (TurnStepCounter >= gameTurnSteps.Count)
            Guard.FireAndForget(StartNewTurn, "resume:StartNewTurn", stallsLoop: true);
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
        startTurnStepHandler?.Cancel();
        playStepHandlerDefault?.Cancel();

        // Detach GameFlow's own subscriptions too: the handler instances stay referenced by these
        // fields, so a late Finished emission would otherwise still reach us.
        if (startTurnStepHandler != null)
            startTurnStepHandler.StartTurnStepFinished -= StartTurnStepFinishedHandler;
        if (playStepHandlerDefault != null)
            playStepHandlerDefault.PlayStepFinished -= PlayCardStepFinishedHandler;
        if (supplyStepHandler != null)
            supplyStepHandler.SupplyStepFinished -= SupplyStepFinishedHandler;
        if (discardStepHandler != null)
            discardStepHandler.DiscardStepFinished -= DiscardStepFinishedHandler;
        if (drawStepHandler != null)
            drawStepHandler.DrawStepFinished -= DrawStepFinishedHandler;

        startTurnStepHandler = null;
        playStepHandlerDefault = null;
        supplyStepHandler = null;
        discardStepHandler = null;
        drawStepHandler = null;
    }

    /// <summary>Drop the shared input-request slot so a stale response cannot be mistaken for a live one.</summary>
    public void ClearCurrentInputRequest() => CurrentInputRequest = null;

    

    private async Task StartTurnStep()
    {
        await BeginStep(TurnStep.START);
        DebugUtilities.PrintPeer("StartTurnStep");
        startTurnStepHandler = new StartTurnStepHandler();
        startTurnStepHandler.StartTurnStepFinished += StartTurnStepFinishedHandler;
        startTurnStepHandler.Start(CurrentFaction);
    }

    public void StartTurnStepFinishedHandler()
    {
        startTurnStepHandler.StartTurnStepFinished -= StartTurnStepFinishedHandler;
        FinishStep(TurnStep.START);
    }

    private async Task PlayCardStep()
    {
        await BeginStep(TurnStep.PLAY_CARD);
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
        DebugUtilities.PrintPeer("VictoryPointStep");
        await vpStepHandler.ProcessVictoryStep(CurrentFaction);
        FinishStep(TurnStep.VICTORY_POINT);
    }

    private async Task DiscardStep()
    {
        await BeginStep(TurnStep.DISCARD);
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
