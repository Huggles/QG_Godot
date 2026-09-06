using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusMareNostrum : StatusCardLogic, IVPModifier
{
    /// <summary>Every Italian navy on the board — one point each.</summary>
    private List<UnitState> ScoringUnits =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(unitState => unitState.Type == UnitType.NAVY)
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits);

    public virtual VPEntry AddVictoryPoints()
    {
        return new VPEntry(ScoringUnits.Count, $"all {FactionState.ForEnum(Faction).FactionData.FactionAdjactiveLabel} navies on the board");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}