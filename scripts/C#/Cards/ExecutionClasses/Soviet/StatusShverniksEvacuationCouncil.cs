using System;
using System.Collections.Generic;
using Godot;

public partial class StatusShverniksEvacuationCouncil : StatusCardLogic, IUnitSupplyModifier
{
    public bool GrantsSupply(UnitState unit)
    {
        return unit.Faction == Faction.SOVIET && unit.Type == UnitType.ARMY;
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}