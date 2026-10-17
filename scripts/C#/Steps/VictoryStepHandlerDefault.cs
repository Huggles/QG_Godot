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

        // Normally seeded by HandleNewTurnStarted. Not after a save restore: the replay rebuilds state
        // from events and never runs GameFlow.StartNewTurn, so the signal that creates this never fires
        // and the resumed turn would reach its victory step with nothing to score into.
        vpTurnSummary ??= new VPTurnSummary(gameFlow.GameTurn);

        vpTurnSummary.Faction = faction;

        ScoreSupplyCountryVPs(faction, vpTurnSummary);
        HandleStatusCardVictoryPoints(faction, vpTurnSummary);

        await new ScorePointsChangeEvent(vpTurnSummary).Apply();

        // The minor factions this faction acts for score in its Victory step, each on its own summary
        // so the points land on the minor faction (and through it on the team). Skipped for one with no
        // pieces left, which has nothing to score and would only add an empty line to the history, and
        // for one whose own Home space an enemy Army holds, as the step itself is (GameFlow.VictoryPointStep).
        foreach (Faction minor in StaticGameData.MinorsOf(faction))
        {
            if (FactionState.ForEnum(minor).OccupiedCountryIds.Count == 0) continue;
            if (BoardState.Live.IsHomeSpaceHeldByEnemy(minor)) continue;

            VPTurnSummary minorSummary = new VPTurnSummary(vpTurnSummary.TurnNumber, minor);
            ScoreSupplyCountryVPs(minor, minorSummary);
            HandleStatusCardVictoryPoints(minor, minorSummary);
            await new ScorePointsChangeEvent(minorSummary).Apply();
        }
    }

    public void ScoreSupplyCountryVPs(Faction scoringFaction, VPTurnSummary summary)
    {
        List<CountryState> occupiedCountries = CountryState.ForIds(FactionState.ForEnum(scoringFaction).OccupiedCountryIds);
        foreach (CountryState cs in occupiedCountries)
        {
            // A Supply space for this faction: a card may add one (Kuibyshev) or deny one (Scorched Earth).
            if (BoardState.Live.IsSupplySpace(cs, scoringFaction))
            {
                int score = Math.Max(3 - cs.Units.Keys.Count, 1);
                VPEntry vPEntry = VPEntry.ForCountry(score, $"supply star on {cs.StaticCountryData.Label}", cs.Id);
                summary.AddScore(vPEntry);
            }
        }
    }
    public void HandleStatusCardVictoryPoints(Faction scoringFaction, VPTurnSummary summary)
    {
        foreach (IVPModifier modifier in BoardState.Live.Modifiers<IVPModifier>().Where(m => m.Faction == scoringFaction))
        {
            summary.AddScore(modifier.AddVictoryPoints(BoardState.Live));
        }
    }

    private async Task ShowVictoryPointEntry(VPEntry vPEntry)
    {
        PresentationServices.Notification.ShowActionText(vPEntry.Description, Faction);
        await Task.Delay(GameSettings.DurationLong);
        PresentationServices.Notification.HideActionText();
    }

    public async Task ScorePoints(VPEntry vPEntry)
    {
        vpTurnSummary.AddScore(vPEntry);
        await ShowVictoryPointEntry(vPEntry);        
    }

    

}
