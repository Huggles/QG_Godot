using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusAbundantResources : StatusCardLogic, IVPModifier
{
    public virtual VPEntry AddVictoryPoints()
    {
        List<Country> countries = [Country.Ukraine, Country.Kazakhstan, Country.Russia];
        int score = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates().Map(unitState => countries.Contains(unitState.CountryState.Country) ? 1 : 0).Sum();
        return new VPEntry(score, $"{score} victory points for armies on Ukraine, Kazakhstan and/or Russia.");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}
