using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusImperoItaliano : StatusCardLogic, IVPModifier
{
    public virtual VPEntry AddVictoryPoints()
    {
        List<Country> countries = [Country.NorthAfrica, Country.Africa, Country.MiddleEast];
        List<Faction> factions = [Faction.GERMANY, Faction.JAPAN, Faction.ITALY];
        int score = factions.Sum((faction) => FactionState.ForEnum(faction).ActiveUnitIds.ToUnitStates().Map(unitState => countries.Contains(unitState.CountryState.Country) ? 1 : 0).Sum());        
        return new VPEntry(score, $"{score} victory points for axis armies in {CountryState.ForEnum(countries[0]).Label},  {CountryState.ForEnum(countries[1]).Label}, and {CountryState.ForEnum(countries[2]).Label}");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}