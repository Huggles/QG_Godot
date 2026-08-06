using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class SupplyStepHandlerDefault : GodotObject, ISupplyStepHandler
{
    private Faction faction { get; set; }
    
    public event Action SupplyStepFinished;

    public void Start(Faction faction)
    {
        this.faction = faction;
        Guard.FireAndForget(ProcessSupplyStep, "SupplyStep", faction);
    }

    private async Task ProcessSupplyStep()
    {
        // StepSkippedException is caught here (and only here) so a player declining a selection is
        // treated as "no supply removal" and the step still completes. Any OTHER exception is left to
        // propagate to Guard, which reports it and deliberately does NOT fire SupplyStepFinished —
        // the loop stays paused until the player chooses Continue.
        try
        {
            await ProcessSupplyStepInternal();
        }
        catch (StepSkippedException)
        {
            DebugUtilities.PrintPeer("Supply step selection skipped");
        }

        SupplyStepFinished?.Invoke();
    }

    private async Task ProcessSupplyStepInternal()
    {
        ErrorInjection.MaybeThrow(ErrorInjection.Site.SupplyStep);

        // Recalculate supply for all units
        GameStateCalculator.CalculateAll();

        // Get active units for this faction and filter for those out of supply
        List<int> activeUnitIds = GameAPI.ActiveUnitsForFaction(this.faction);
        List<UnitState> outOfSupplyUnits = UnitState.ForIds(activeUnitIds)
            .Where(unit => !unit.InSupply)
            .ToList();

        // Remove out-of-supply units
        if (outOfSupplyUnits.Count > 0)
        {
            string message = $"{outOfSupplyUnits.Count} unit(s) removed due to lack of supply";
            PresentationServices.Notification.ShowActionText(message, this.faction);
            DebugUtilities.PrintPeer($"Removing {outOfSupplyUnits.Count} out-of-supply units for {this.faction}");
            
            // Create removal events for each out-of-supply unit
            foreach (var unit in outOfSupplyUnits)
            {
                RemoveUnitChangeEvent removeEvent = new RemoveUnitChangeEvent(
                    unit.Faction, 
                    unit.Id, 
                    UnitRemovalReason.SUPPLY
                );
                DebugUtilities.PrintPeer($"  - Removing {unit.Faction} {unit.Type} from {CountryState.ForId(unit.CountryId).Label}");
                await removeEvent.ApplyChange();
            }
            
            // Give player time to see the changes
            await Task.Delay(GameSettings.DurationMedium);
        }
        else
        {
            // No units out of supply
            await Task.Delay(GameSettings.DurationShort);
        }
    }
}
