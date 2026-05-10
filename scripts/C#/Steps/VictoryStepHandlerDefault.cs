using Godot;
using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;

public partial class VictoryStepHandlerDefault : IVictoryStepHandler
{
    private VPTurnSummary vpTurnSummary;

    private GameFlow gameFlow { get { return GameSession.Instance.GameFlow; } }
    private Faction Faction;
    private FactionState FactionState => GameSession.FactionStates[Faction];

    public VictoryStepHandlerDefault()
    {
        EventBus.Instance.NewTurnStarted += HandleNewRound;
    }
    
    public void HandleNewRound(int roundNumber)
    {
        vpTurnSummary = new VPTurnSummary(gameFlow.Round);
        Faction = gameFlow.CurrentFaction;
    }

    public async Task ProcessVictoryStep(Faction faction)
    {
        Faction = faction;

        await ScoreSupplyCountryVPs();
        await HandleStatusCardVictoryPoints();

        FactionState.Score += vpTurnSummary.TotalScore;
        gameFlow.VictoryPointSummaries[Faction].Add(vpTurnSummary);
        EventBus.Emit(EventBus.SignalName.FactionScoredPoints, (int)Faction, FactionState.Score);

        DebugUtilities.PrintPeer(vpTurnSummary, DebugVerbosity.INFO);
    }

    public async Task ScoreSupplyCountryVPs()
    {
        foreach (CountryState cs in CountryState.ForIds(FactionState.OccupiedCountryIds))
        {
            if (cs.IsSupply)
            {
                int score = Math.Max(3 - cs.Units.Keys.Count, 1);
                VPEntry vPEntry = new VPEntry(score, $"{score} VP for supply star on {cs.StaticCountryData.Label}");
                vpTurnSummary.AddScore(vPEntry);
                await ShowVictoryPointEntry(vPEntry);
            }
        }
    }
    public async Task HandleStatusCardVictoryPoints()
    {
        foreach (CardState cardState in DeckState.ForFaction(Faction).StatusCardStates)
        {
            if (cardState.CardLogic.CanBeActivated() && cardState.CardLogic is IStatusVictoryPoints statusVictoryPoints)
            {
                VPEntry vPEntry = statusVictoryPoints.AddVictoryPoints();
                vpTurnSummary.AddScore(statusVictoryPoints.AddVictoryPoints());
                await ShowVictoryPointEntry(vPEntry);
            }
        }
    }

    private async Task ShowVictoryPointEntry(VPEntry vPEntry)
    {
        PlayerActionLabel.ShowText(vPEntry.Reason, Faction);
        await Task.Delay(GameSettings.PauseDuration);
        PlayerActionLabel.HideText();        
    }

    public async Task ScorePoints(VPEntry vPEntry)
    {
        vpTurnSummary.AddScore(vPEntry);
        await ShowVictoryPointEntry(vPEntry);        
    }

    

}
