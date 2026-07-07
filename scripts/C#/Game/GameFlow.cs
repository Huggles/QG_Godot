using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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
                CardsPlayedThisTurnStep.Clear();
                GameTurnStep gameTurnStep = gameTurnSteps[field - 1];
                if(Multiplayer.IsServer())
                {                    
                    gameTurnStep.Handler();                    
                }
                EventBus.Emit(EventBus.SignalName.NextStepStarted, (int)gameTurnStep.TurnStep);
            }
        } } = 0;
    [Export] public TurnStep TurnStep { get; set; } = 0;

    public int Round => ((GameTurn - 1) / StaticGameData.PlayableFactions.Count) + 1;
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

    public void StartGame()
    {
        DebugUtilities.PrintPeer("GameFlow: Starting game");
        foreach (FactionState faction in gameState.PlayableFactionStates)
        {
            DrawCardsChangeEvent drawCardsChangeEvent = new DrawCardsChangeEvent(Faction.NONE, faction.Faction, 7, false);
            drawCardsChangeEvent.IsTrigger = false;
            _ = CardPlayPool.DoChangeEvent(drawCardsChangeEvent);
        }

        GameStateCalculator.CalculateAll();

        foreach (Faction faction in StaticGameData.PlayableFactions)
        {
            VictoryPointSummaries[faction] = new List<VPTurnSummary>();
        }

        GameStarted = true;

        
        // Only fade the local (host) player's loading screen here.
        // Client loading screens are faded via GameSession.ReceiveGameStarted RPC.
        _ = StartNewTurn();
    }

    private async Task StartNewTurn()
    {
        DebugUtilities.PrintPeer("StartNewTurn");
        await new ChangeRoundChangeEvent(GameTurn + 1).ApplyChange();
        TurnStepCounter = 0;
        DebugUtilities.PrintPeer($"Game turn: {GameTurn} ( {Enum.GetName(typeof(Faction), CurrentFaction)} / {Enum.GetName(typeof(FactionTeam), CurrentFactionTeam)} )");
        EventBus.Emit(EventBus.SignalName.NewTurnStarted, GameTurn);
        await Task.Delay(100);        
        StartNextStep();
    }

    public void StartNextStep()
    {
        DebugUtilities.PrintPeer("StartNextStep");
        TurnStepCounter++;
    }

    

    private async Task StartTurnStep()
    {
        await new ChangeStepChangeEvent(TurnStep.START).ApplyChange();
        DebugUtilities.PrintPeer("StartTurnStep");
        startTurnStepHandler = new StartTurnStepHandler();
        startTurnStepHandler.StartTurnStepFinished += StartTurnStepFinishedHandler;
        startTurnStepHandler.Start(CurrentFaction);
    }

    public void StartTurnStepFinishedHandler()
    {
        startTurnStepHandler.StartTurnStepFinished -= StartTurnStepFinishedHandler;
        StartNextStep();
    }

    private async Task PlayCardStep()
    {
        await new ChangeStepChangeEvent(TurnStep.PLAY_CARD).ApplyChange();
        DebugUtilities.PrintPeer("PlayCardStep");
        playStepHandlerDefault = new PlayStepHandlerDefault();
        playStepHandlerDefault.PlayStepFinished += PlayCardStepFinishedHandler;
        playStepHandlerDefault.Start(CurrentFaction);
    }

    public void PlayCardStepFinishedHandler()
    {
        playStepHandlerDefault.PlayStepFinished -= PlayCardStepFinishedHandler;
        StartNextStep();
    }

    private async Task SupplyStep()
    {
        await new ChangeStepChangeEvent(TurnStep.SUPPLY).ApplyChange();
        DebugUtilities.PrintPeer("SupplyStep");
        supplyStepHandler = new SupplyStepHandlerDefault();
        supplyStepHandler.SupplyStepFinished += SupplyStepFinishedHandler;
        supplyStepHandler.Start(CurrentFaction);
    }

    public void SupplyStepFinishedHandler()
    {
        supplyStepHandler.SupplyStepFinished -= SupplyStepFinishedHandler;
        StartNextStep();
    }

    private async Task VictoryPointStep()
    {
        await new ChangeStepChangeEvent(TurnStep.VICTORY_POINT).ApplyChange();
        DebugUtilities.PrintPeer("VictoryPointStep");
        await vpStepHandler.ProcessVictoryStep(CurrentFaction);
        StartNextStep();
    }

    private async Task DiscardStep()
    {
        await new ChangeStepChangeEvent(TurnStep.DISCARD).ApplyChange();
        DebugUtilities.PrintPeer("DiscardStep");
        discardStepHandler = new DiscardStepHandlerDefault();
        discardStepHandler.DiscardStepFinished += DiscardStepFinishedHandler;
        discardStepHandler.Start(CurrentFaction);
    }

    public void DiscardStepFinishedHandler()
    {
        discardStepHandler.DiscardStepFinished -= DiscardStepFinishedHandler;
        StartNextStep();
    }

    private async Task DrawStep()
    {
        await new ChangeStepChangeEvent(TurnStep.DRAW).ApplyChange();
        DebugUtilities.PrintPeer("DrawStep");
        drawStepHandler = new DrawStepHandlerDefault();
        drawStepHandler.DrawStepFinished += DrawStepFinishedHandler;
        drawStepHandler.Start(CurrentFaction);
    }

    public void DrawStepFinishedHandler()
    {
        drawStepHandler.DrawStepFinished -= DrawStepFinishedHandler;
        StartNextStep();
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
