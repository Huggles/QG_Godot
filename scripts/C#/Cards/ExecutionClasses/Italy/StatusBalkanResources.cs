using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusBalkanResources : StatusCardLogic, IVPModifier
{
    public virtual VPEntry AddVictoryPoints()
    {
        List<Country> countries = [Country.Balkans];
        List<Faction> factions = [Faction];
        int score = factions.Sum((faction) => FactionState.ForEnum(faction).ActiveUnitIds.ToUnitStates().Map(unitState => countries.Contains(unitState.CountryState.Country) ? 1 : 0).Sum());        
        return new VPEntry(score, $"{score} victory points for italian army in {CountryState.ForEnum(countries[0]).Label}");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}