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
        int score = factions.Sum((faction) => FactionState.ForEnum(faction).ActiveUnitIds.ToUnitStates()
            .Count(unitState => countries.Contains(unitState.CountryState.Country) && unitState.Type == UnitType.ARMY));
        return new VPEntry(score, $"axis armies in {CountryState.ForEnum(countries[0]).Label}, {CountryState.ForEnum(countries[1]).Label} and {CountryState.ForEnum(countries[2]).Label}");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}