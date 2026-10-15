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
        Guard.FireAndForget(ProcessSupplyStep, "SupplyStep", faction, stallsLoop: true);
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
        ErrorInjection.MaybeReportAndRethrow(ErrorInjection.Site.ReportedRethrow);

        // Recalculate supply for all units
        GameStateCalculator.CalculateAll();

        // The faction whose turn it is, then the minor factions it acts for: France and China trace
        // their own supply, but their patron's Supply step is when their unsupplied pieces go. One
        // calculation covers them all — no faction traces supply through another's pieces, so removing
        // one faction's units cannot change another's answer.
        bool removedAny = false;
        foreach (Faction checkedFaction in StaticGameData.MinorsOf(this.faction).Prepend(this.faction))
            removedAny |= await RemoveOutOfSupplyUnits(checkedFaction);

        // Give player time to see the changes
        await Task.Delay(removedAny ? GameSettings.DurationMedium : GameSettings.DurationShort);
    }

    /// <summary>Remove <paramref name="checkedFaction"/>'s out-of-supply units. True if there were any.</summary>
    private async Task<bool> RemoveOutOfSupplyUnits(Faction checkedFaction)
    {
        // Get active units for this faction and filter for those out of supply. A unit protected this
        // turn ("do not remove that Army this turn") stays.
        List<int> activeUnitIds = GameAPI.ActiveUnitsForFaction(checkedFaction);
        List<UnitState> outOfSupplyUnits = UnitState.ForIds(activeUnitIds)
            .Where(unit => !unit.InSupply && !BoardState.Live.ImmuneForTurn(unit))
            .ToList();

        if (outOfSupplyUnits.Count == 0) return false;

        string message = $"{checkedFaction.WithPlayer()} loses {outOfSupplyUnits.Count} unit(s) to lack of supply";
        await new ShowActionLabelPresentationEvent(checkedFaction, message).Apply();
        DebugUtilities.PrintPeer($"Removing {outOfSupplyUnits.Count} out-of-supply units for {checkedFaction}");

        // Each removal goes through the reaction pipeline, so a card that answers a removal (Leningrad,
        // Élan, Retreat and Regroup) may be used; its windows open only for a faction holding one.
        foreach (var unit in outOfSupplyUnits)
        {
            // Asked again per unit: a reaction to an earlier removal may have protected or resupplied it.
            if (unit.InSupply || BoardState.Live.ImmuneForTurn(unit) || !BoardState.Live.IsDeployed(unit)) continue;

            RemoveUnitChangeEvent removeEvent = new RemoveUnitChangeEvent(
                unit.Faction,
                unit.Id,
                UnitRemovalReason.SUPPLY
            );
            DebugUtilities.PrintPeer($"  - Removing {unit.Faction} {unit.Type} from {CountryState.ForId(unit.CountryId).Label}");
            await CardPlayPool.DoChangeEvent(removeEvent);
        }
        return true;
    }
}
