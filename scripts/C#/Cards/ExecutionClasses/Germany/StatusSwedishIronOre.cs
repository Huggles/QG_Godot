using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusSwedishIronOre : StatusCardLogic, IVPModifier
{
    public virtual VPEntry AddVictoryPoints()
    {
        List<Country> countries = [Country.Scandinavia, Country.BalticSea];
        int score = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates().Map(unitState => countries.Contains(unitState.CountryState.Country) ? 1 : 0).Sum();
        return new VPEntry(score, $"{score} victory points for a navy in the Baltic Sea and army in Scandinavia.");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}
