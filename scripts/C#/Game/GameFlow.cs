using Godot;
using System;
using System.Collections.Generic;

public partial class GameFlow : GodotObject
{
    private bool GameStarted { get; set; } = false;
    public int GameTurn { get; set; } = 0;
    public int TurnStep { get; set; } = 0;

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

    private List<Action> turnStepMethods;
    private IVictoryStepHandler vpStepHandler = new VictoryStepHandlerDefault();
    public Dictionary<Faction, List<VPTurnSummary>> VictoryPointSummaries = new Dictionary<Faction, List<VPTurnSummary>>();

    public GameFlow(){
        turnStepMethods = new List<Action> {
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
        TurnStep++;
        turnStepMethods[TurnStep - 1]();
        EventBus.Emit("NextStepStarted",TurnStep);
    }

    private void StartNewTurn()
    {
        GD.Print("StartNewTurn");
        GameTurn += 1;
        TurnStep = 0;
        GD.Print($"Game turn: {GameTurn} ( {Enum.GetName(typeof(Faction), CurrentFaction)} / {Enum.GetName(typeof(FactionTeam), CurrentFactionTeam)} )");
        EventBus.Emit("NewTurnStarted", GameTurn);        
        StartNextStep();
    }

    private void StartTurnStep()
    {
        GD.Print("_start_turn_step");
        ProgressGame();
    }

    private async void PlayCardStep()
    {
        GD.Print("PlayCardStep");
        PlayStepHandlerDefault playStepHandlerDefault = new PlayStepHandlerDefault();
        playStepHandlerDefault.Start(CurrentFaction);        
        await ToSignal(playStepHandlerDefault, PlayStepHandlerDefault.SignalName.PlayStepFinished); 
        
        ProgressGame();
    }

    private void SupplyStep()
    {
        GD.Print("SupplyStep");
        EventBus.Emit("RecalculateSupply", GameTurn);                
        ProgressGame();
    }

    private void VictoryPointStep()
    {
        GD.Print("VictoryPointStep");
        vpStepHandler.ProcessVictoryStep(CurrentFaction);
        ProgressGame();
    }

    private void DiscardStep()
    {
        GD.Print("DiscardStep");
        ProgressGame();
    }

    private void DrawStep()
    {
        GD.Print("DrawStep");
        CurrentFactionDeckState.DebugHand();
        CurrentFactionDeckState.DrawCards(7 - CurrentFactionDeckState.HandCardIds.Count);
        CurrentFactionDeckState.DebugHand();
        ProgressGame();
    }
}
