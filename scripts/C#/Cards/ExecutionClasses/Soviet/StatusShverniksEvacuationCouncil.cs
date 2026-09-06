using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusShverniksEvacuationCouncil : StatusCardLogic, IUnitSupplyModifier
{
    public bool GrantsSupply(UnitState unit)
    {
        return unit.Faction == Faction.SOVIET && unit.Type == UnitType.ARMY;
    }

    /// <summary>The Soviet Armies this keeps alive — shown as the ones currently OUT of supply,
    /// which is what the card is actually doing for you right now. Every Soviet Army benefits in
    /// principle; lighting all of them would say nothing.</summary>
    public override TargetSet Targets() =>
        TargetSet.Units(FactionState.ForEnum(Faction).UnsuppliedUnitIds.ToUnitStates()
            .Where(GrantsSupply).ToList());

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}