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
        get => ((GameTurn - 1) / Enum.GetNames(typeof(Faction)).Length) + 1;
    }

    public Faction CurrentFaction
    {
        get
        {
            if (GameTurn > 0)
            {
                int factionInt = (GameTurn - 1) % Enum.GetNames(typeof(Faction)).Length;
                return (Faction)factionInt;
            }
            else
            {
                return Faction.GERMANY;
            }
        }
    }

    private GameState gameState { 
        get { return GameSession.Instance.GameState;}
    }

    public FactionState CurrentFactionState => GameSession.FactionStates[CurrentFaction];
    public DeckState CurrentFactionDeckState => DeckState.ForFaction(CurrentFaction);

    public FactionTeam CurrentFactionTeam =>
        (GameTurn > 0 && GameTurn % 2 == 0) ? FactionTeam.ALLIES : FactionTeam.AXIS;

    private List<Func<Task>> turnStepMethods;
    private IVictoryStepHandler vpStepHandler = new VictoryStepHandlerDefault();
    public Dictionary<Faction, List<VPTurnSummary>> VictoryPointSummaries = new Dictionary<Faction, List<VPTurnSummary>>();

    public GameFlow(){
        turnStepMethods = new List<Func<Task>> {
            StartTurnStep,
            PlayCardStep,
            SupplyStep,
            VictoryPointStep,
            DiscardStep,
            DrawStep,
            StartNewTurn 
        };
    }

    public void StartGame()
    {
        foreach (FactionState faction in gameState.FactionStates.Values)
        {
            faction.DeckState.DrawCards(7);
        }

        EventBus.Emit("RecalculateSupply");

        foreach (Faction faction in Enum.GetValues(typeof(Faction)))
        {
            VictoryPointSummaries[faction] = new List<VPTurnSummary>();
        }

        GameStarted = true;
        StartNewTurn();
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
        turnStepMethods[TurnStepCounter - 1]();
        EventBus.Emit("NextStepStarted",TurnStepCounter);
    }

    private async Task StartNewTurn()
    {
        GD.Print("StartNewTurn");
        GameTurn += 1;
        TurnStepCounter = 0;
        GD.Print($"Game turn: {GameTurn} ( {Enum.GetName(typeof(Faction), CurrentFaction)} / {Enum.GetName(typeof(FactionTeam), CurrentFactionTeam)} )");
        EventBus.Emit(EventBus.SignalName.NewTurnStarted, GameTurn);        
        StartNextStep();
    }

    private async Task StartTurnStep()
    {        
        GD.Print("_start_turn_step");
        this.TurnStep = TurnStep.START;
        StartTurnStepHandler startTurnStepHandler = new StartTurnStepHandler();
        startTurnStepHandler.Start(CurrentFaction);
        await ToSignal(startTurnStepHandler, StartTurnStepHandler.SignalName.StartTurnStepFinished); 
        ProgressGame();
    }

    private async Task PlayCardStep()
    {
        GD.Print("PlayCardStep");
        this.TurnStep = TurnStep.PLAY_CARD;
        PlayStepHandlerDefault playStepHandlerDefault = new PlayStepHandlerDefault();
        playStepHandlerDefault.Start(CurrentFaction);        
        await ToSignal(playStepHandlerDefault, PlayStepHandlerDefault.SignalName.PlayStepFinished); 
        
        ProgressGame();
    }

    private async Task SupplyStep()
    {
        this.TurnStep = TurnStep.SUPPLY;
        GD.Print("SupplyStep");
        EventBus.Emit("RecalculateSupply", GameTurn);                
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
        ProgressGame();
    }

    private async Task DrawStep()
    {
        GD.Print("DrawStep");
        this.TurnStep = TurnStep.DRAW;
        CurrentFactionDeckState.DrawCards(7 - CurrentFactionDeckState.HandCardIds.Count);
        //CurrentFactionDeckState.DebugHand();
        ProgressGame();
    }
}
