using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class GameFlow : GodotObject
{
    private bool GameStarted { get; set; } = false;
    public int GameTurn { get; set; } = 0;
    public int TurnStepCounter { get; set; } = 0;
    public TurnStep TurnStep { get; set; } = 0;

    public int Round
    {
        get => ((GameTurn - 1) / StaticGameData.PlayableFactions.Count) + 1;
    }

    public Faction CurrentFaction
    {
        get
        {
            if (GameTurn > 0)
            {
                int factionInt = (GameTurn - 1) % StaticGameData.PlayableFactions.Count;
                return StaticGameData.PlayableFactions[factionInt];
            }
            else
            {
                return Faction.GERMANY;
            }
        }
    }

    private GameState gameState
    {
        get { return GameSession.Instance.GameState; }
    }

    public FactionState CurrentFactionState => GameSession.FactionStates[CurrentFaction];
    public DeckState CurrentFactionDeckState => DeckState.ForFaction(CurrentFaction);

    public FactionTeam CurrentFactionTeam =>
        (GameTurn > 0 && GameTurn % 2 == 0) ? FactionTeam.ALLIES : FactionTeam.AXIS;

    
    
    public Dictionary<Faction, List<VPTurnSummary>> VictoryPointSummaries = new Dictionary<Faction, List<VPTurnSummary>>();

    private List<GameTurnStep> gameTurnSteps;
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

    public void StartGame()
    {
        foreach (FactionState faction in gameState.FactionStates.Values)
        {
            faction.DeckState.DrawCards(7);
        }

        GameStateCalculator.CalculateAll();

        foreach (Faction faction in StaticGameData.PlayableFactions)
        {
            VictoryPointSummaries[faction] = new List<VPTurnSummary>();
        }

        GameStarted = true;

        DebugUtilities.PrintPeer("GameFlow: Starting game");
        foreach (PlayerScene playerScene in PlayerFactionRegistry.GetAllPlayers())
        {
            playerScene.FadeLoadingScreen();
        }

        _ = StartNewTurn();
    }

    public void ProgressGame()
    {
        GD.Print($"ProgressGame: {GameTurn}");
        StartNextStep();
    }

    private void StartNextStep()
    {
        GD.Print("StartNextStep");
        TurnStepCounter++;
        GameTurnStep gameTurnStep = gameTurnSteps[TurnStepCounter - 1];
        gameTurnStep.Handler();
        EventBus.Emit(EventBus.SignalName.NextStepStarted, (int)gameTurnStep.TurnStep);
    }

    private async Task StartNewTurn()
    {
        GD.Print("StartNewTurn");
        GameTurn += 1;
        TurnStepCounter = 0;
        GD.Print($"Game turn: {GameTurn} ( {Enum.GetName(typeof(Faction), CurrentFaction)} / {Enum.GetName(typeof(FactionTeam), CurrentFactionTeam)} )");
        EventBus.Emit(EventBus.SignalName.NewTurnStarted, GameTurn);
        await Task.Delay(100);        
        StartNextStep();
    }

    private async Task StartTurnStep()
    {
        GD.Print("_start_turn_step");
        this.TurnStep = TurnStep.START;
        startTurnStepHandler = new StartTurnStepHandler();
        startTurnStepHandler.StartTurnStepFinished += StartTurnStepFinishedHandler;
        startTurnStepHandler.Start(CurrentFaction);        
    }

    private void StartTurnStepFinishedHandler()
    {
        startTurnStepHandler.StartTurnStepFinished -= StartTurnStepFinishedHandler;
        ProgressGame();
    }

    private async Task PlayCardStep()
    {
        GD.Print("PlayCardStep");
        this.TurnStep = TurnStep.PLAY_CARD;
        playStepHandlerDefault = new PlayStepHandlerDefault();
        playStepHandlerDefault.PlayStepFinished += PlayCardStepFinishedHandler;
        playStepHandlerDefault.Start(CurrentFaction);
    }
    private void PlayCardStepFinishedHandler()
    {
        playStepHandlerDefault.PlayStepFinished -= PlayCardStepFinishedHandler;
        ProgressGame();
    }

    private async Task SupplyStep()
    {
        this.TurnStep = TurnStep.SUPPLY;
        GD.Print("SupplyStep");
        supplyStepHandler = new SupplyStepHandlerDefault();
        supplyStepHandler.SupplyStepFinished += SupplyStepFinishedHandler;
        supplyStepHandler.Start(CurrentFaction);
    }

    private void SupplyStepFinishedHandler()
    {
        supplyStepHandler.SupplyStepFinished -= SupplyStepFinishedHandler;
        ProgressGame();
    }

    private async Task VictoryPointStep()
    {
        GD.Print("VictoryPointStep");
        this.TurnStep = TurnStep.VICTORY_POINT;
        await vpStepHandler.ProcessVictoryStep(CurrentFaction);
        ProgressGame();
    }

    private async Task DiscardStep()
    {
        GD.Print("DiscardStep");
        this.TurnStep = TurnStep.DISCARD;
        discardStepHandler = new DiscardStepHandlerDefault();
        discardStepHandler.DiscardStepFinished += DiscardStepFinishedHandler;
        discardStepHandler.Start(CurrentFaction);
    }

    private void DiscardStepFinishedHandler()
    {
        discardStepHandler.DiscardStepFinished -= DiscardStepFinishedHandler;
        ProgressGame();
    }

    private async Task DrawStep()
    {
        GD.Print("DrawStep");
        this.TurnStep = TurnStep.DRAW;
        drawStepHandler = new DrawStepHandlerDefault();
        drawStepHandler.DrawStepFinished += DrawStepFinishedHandler;
        drawStepHandler.Start(CurrentFaction);
    }

    private void DrawStepFinishedHandler()
    {
        drawStepHandler.DrawStepFinished -= DrawStepFinishedHandler;
        ProgressGame();
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
