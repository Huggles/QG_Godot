using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusForwardBases : StatusCardLogic, IStatusVictoryPoints
{
    public virtual VPEntry AddVictoryPoints()
    {
        List<Country> countries = [Country.Hawaii, Country.PacificNorthWest, Country.NewZealand];
        List<Faction> factions = [Faction];
        int score = factions.Sum((faction) => FactionState.ForEnum(faction).ActiveUnitIds.ToUnitStates().Map(unitState => countries.Contains(unitState.CountryState.Country) ? 2 : 0).Sum());
        score = Math.Min(score, 2);
        return new VPEntry(score, $"{score} victory points for {FactionState.ForEnum(Faction).FactionData.FactionAdjactiveLabel} army in {CountryState.ForEnum(countries[0]).Label},  {CountryState.ForEnum(countries[1]).Label} or  {CountryState.ForEnum(countries[2]).Label}.");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}