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
        _ = ProcessSupplyStep();
    }

    private async Task ProcessSupplyStep()
    {
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
            PlayerActionLabel.ShowText(message, this.faction);
            DebugUtilities.PrintPeer($"Removing {outOfSupplyUnits.Count} out-of-supply units for {this.faction}", DebugVerbosity.INFO);
            
            // Create removal events for each out-of-supply unit
            foreach (var unit in outOfSupplyUnits)
            {
                RemoveUnitChangeEvent removeEvent = new RemoveUnitChangeEvent(
                    unit.Faction, 
                    unit.Id, 
                    UnitRemovalReason.SUPPLY
                );
                DebugUtilities.PrintPeer($"  - Removing {unit.Faction} {unit.Type} from {CountryState.ForId(unit.CountryId).Label}", DebugVerbosity.INFO);
                await removeEvent.ApplyChange();
            }
            
            // Give player time to see the changes
            await Task.Delay(GameSettings.PauseDuration);
        }
        else
        {
            // No units out of supply
            await Task.Delay(GameSettings.PauseDuration);
        }

        SupplyStepFinished?.Invoke();
    }
}
