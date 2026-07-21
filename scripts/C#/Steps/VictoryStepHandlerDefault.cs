using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;

public partial class VictoryStepHandlerDefault : IVictoryStepHandler
{
    private VPTurnSummary vpTurnSummary;

    private GameFlow gameFlow { get { return GameFlow.Instance; } }
    private Faction Faction;
    private FactionState FactionState => FactionState.ForEnum(Faction);

    public VictoryStepHandlerDefault()
    {
        EventBus.Instance.NewTurnStarted += HandleNewTurnStarted;
    }
    
    public void HandleNewTurnStarted(int turnNumber)
    {        
        Faction = gameFlow.CurrentFaction;
        vpTurnSummary = new VPTurnSummary(turnNumber);
    }

    public async Task ProcessVictoryStep(Faction faction)
    {
        Faction = faction;

        ScoreSupplyCountryVPs();
        HandleStatusCardVictoryPoints();

        await new ScorePointsChangeEvent(vpTurnSummary).ApplyChange();
    }

    public void ScoreSupplyCountryVPs()
    {
        List<CountryState> occupiedCountries = CountryState.ForIds(FactionState.OccupiedCountryIds);
        foreach (CountryState cs in occupiedCountries)
        {
            if (cs.IsSupply)
            {
                int score = Math.Max(3 - cs.Units.Keys.Count, 1);
                VPEntry vPEntry = new VPEntry(score, $"{score} VP for supply star on {cs.StaticCountryData.Label}");
                vpTurnSummary.AddScore(vPEntry);                
            }
        }
    }
    public void HandleStatusCardVictoryPoints()
    {
        foreach (IVPModifier modifier in ModifierRegistry.GetAll<IVPModifier>().Where(m => m.Faction == Faction))
        {
            vpTurnSummary.AddScore(modifier.AddVictoryPoints());
        }
    }

    private async Task ShowVictoryPointEntry(VPEntry vPEntry)
    {
        PlayerActionLabel.ShowText(vPEntry.Reason, Faction);
        await Task.Delay(GameSettings.DurationLong);
        PlayerActionLabel.HideText();        
    }

    public async Task ScorePoints(VPEntry vPEntry)
    {
        vpTurnSummary.AddScore(vPEntry);
        await ShowVictoryPointEntry(vPEntry);        
    }

    

}
