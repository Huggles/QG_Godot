using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusAntiCommunistSentiment : StatusCardLogic, IVPModifier
{
    public virtual VPEntry AddVictoryPoints()
    {
        List<Country> countries = [Country.Ukraine, Country.Russia];
        List<Faction> factions = [Faction];
        int score = factions.Sum((faction) => FactionState.ForEnum(faction).ActiveUnitIds.ToUnitStates().Map(unitState => countries.Contains(unitState.CountryState.Country) ? 1 : 0).Sum());        
        return new VPEntry(score, $"{FactionState.ForEnum(Faction).FactionData.FactionAdjactiveLabel} armies in {CountryState.ForEnum(countries[0]).Label} and {CountryState.ForEnum(countries[1]).Label}");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}