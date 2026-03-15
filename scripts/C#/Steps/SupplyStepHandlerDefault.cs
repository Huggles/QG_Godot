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
        
        // Find all units that are out of supply
        List<UnitState> outOfSupplyUnits = new List<UnitState>();
        
        foreach (var unitState in GameSession.Instance.GameState.UnitStatesById.Values)
        {
            // Only check units that are on the board (have a country)
            if (unitState.CountryId >= 0 && !unitState.InSupply)
            {
                outOfSupplyUnits.Add(unitState);
            }
        }

        // Remove out-of-supply units
        if (outOfSupplyUnits.Count > 0)
        {
            string message = $"{outOfSupplyUnits.Count} unit(s) removed due to lack of supply";
            PlayerActionLabel.ShowText(message, Faction.NONE);
            GD.Print($"Removing {outOfSupplyUnits.Count} out-of-supply units");
            
            // Create removal events for each out-of-supply unit
            foreach (var unit in outOfSupplyUnits)
            {
                RemoveUnitChangeEvent removeEvent = new RemoveUnitChangeEvent(
                    unit.Faction, 
                    unit.Id, 
                    UnitRemovalReason.SUPPLY
                );
                GD.Print($"  - Removing {unit.Faction} {unit.Type} from {CountryState.ForId(unit.CountryId).Label}");
                await removeEvent.ApplyChange();
            }
            
            // Give player time to see the changes
            await Task.Delay(2000);
        }
        else
        {
            // No units out of supply
            await Task.Delay(500);
        }

        SupplyStepFinished?.Invoke();
    }
}
