using Godot;
using System;

public partial class VictoryStepHandlerDefault : IVictoryStepHandler
{
     private VPTurnSummary vpTurnSummary;

    private GameFlow gameFlow { get { return GameSession.Instance.GameFlow; } }

    public void ProcessVictoryStep(Faction faction)
    {
        vpTurnSummary = new VPTurnSummary(GameSession.Instance.GameFlow.Round);

        var factionState = GameSession.FactionStates[faction];

        foreach (CountryState cs in CountryState.ForIds(factionState.OccupiedCountryIds))
        {
            if (cs.IsSupply)
            {
                int score = Math.Max(3 - cs.Units.Keys.Count, 1);
                vpTurnSummary.AddScore(score, $"supply star on {cs.StaticCountryData.Label}");
            }
        }

        factionState.Score += vpTurnSummary.TotalScore;
        gameFlow.VictoryPointSummaries[faction].Add(vpTurnSummary);

        EventBus.Emit(EventBus.SignalName.FactionScoredPoints, (int)faction, vpTurnSummary.TotalScore);

        DebugUtilities.PrintPeer(vpTurnSummary);
    }
}
