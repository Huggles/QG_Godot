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

    public FactionState CurrentFactionState => gameState.FactionStateForEnum(CurrentFaction);

    public DeckState CurrentFactionDeckState => DeckState.ForFaction(CurrentFaction);

    public FactionTeam CurrentFactionTeam =>
        (GameTurn > 0 && GameTurn % 2 == 0) ? FactionTeam.ALLIES : FactionTeam.AXIS;

    private List<Action> turnStepMethods;
    private IVictoryStepHandler vpStepHandler = new VictoryStepHandlerDefault();
    private Dictionary<Faction, List<object>> victoryPointSummaries = new Dictionary<Faction, List<object>>();

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
            victoryPointSummaries[faction] = new List<object>();
        }

        GameStarted = true;
        StartNewTurn();
    }

    public void ProgressGame()
    {
        GD.Print($"progress_game: {GameTurn}");
        StartNextStep();
    }

    private void StartNextStep()
    {
        GD.Print("_start_next_step");
        TurnStep++;
        turnStepMethods[TurnStep - 1]();
        EventBus.Emit("NextStepStarted",TurnStep);
    }

    private void StartNewTurn()
    {
        GD.Print("_start_new_turn");
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
        GD.Print("_play_card_step");
        // GameManager.GameState.CardPlayHandler.RequestCardPlay();        
        await EventBus.GetSignalAwaiter("CardPlayHandlerCompleted");
        ProgressGame();
    }

    private void SupplyStep()
    {
        GD.Print("_supply_step");
        EventBus.Emit("RecalculateSupply", GameTurn);                
        ProgressGame();
    }

    private void VictoryPointStep()
    {
        GD.Print("_victory_point_step");
        vpStepHandler.ProcessVictoryStep(CurrentFaction);
        ProgressGame();
    }

    private void DiscardStep()
    {
        GD.Print("_discard_step");
        ProgressGame();
    }

    private void DrawStep()
    {
        GD.Print("_draw_step");
        CurrentFactionDeckState.DebugHand();
        CurrentFactionDeckState.DrawCards(7 - CurrentFactionDeckState.HandCardIds.Count);
        CurrentFactionDeckState.DebugHand();
        ProgressGame();
    }
}
