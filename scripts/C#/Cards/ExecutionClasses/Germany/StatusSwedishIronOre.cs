using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusSwedishIronOre : StatusCardLogic, IVPModifier
{
    public virtual VPEntry AddVictoryPoints()
    {
        int score = 0;
        if (FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates().Any(u => u.CountryState.Country == Country.BalticSea && u.Type == UnitType.NAVY)) score++;
        if (FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates().Any(u => u.CountryState.Country == Country.Scandinavia && u.Type == UnitType.ARMY)) score++;
        return new VPEntry(score, "a navy in the Baltic Sea and army in Scandinavia");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}
