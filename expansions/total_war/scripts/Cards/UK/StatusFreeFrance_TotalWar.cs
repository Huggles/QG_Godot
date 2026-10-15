using System.Collections.Generic;
using System.Linq;

/// <summary>Passive: no trigger and no steps, only the supply grant.</summary>
public partial class StatusFreeFrance_TotalWar : StatusCardLogic, IUnitSupplyModifier
{
    public bool GrantsSupply(BoardState board, UnitState unit) => unit.Faction == Faction.FRANCE;

    public override TargetSet Targets() => TargetSet.Units(BoardState.Live.ActiveUnits(Faction.FRANCE));
}
