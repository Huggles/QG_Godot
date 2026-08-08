using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusMareNostrum : StatusCardLogic, IVPModifier
{
    public virtual VPEntry AddVictoryPoints()
    {
        int score = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Count(unitState => unitState.Type == UnitType.NAVY);
        return new VPEntry(score, $"all {FactionState.ForEnum(Faction).FactionData.FactionAdjactiveLabel} navies on the board");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}