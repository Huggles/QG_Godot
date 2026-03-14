using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusMackenzieKingDraftstheNationalResourcesMobilizationAct : StatusCardLogic, IStatusVictoryPoints
{
    public virtual VPEntry AddVictoryPoints()
    {
        List<Country> countries = [Country.NorthAtlantic, Country.Canada];
        int score = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates().Map(unitState => countries.Contains(unitState.CountryState.Country) ? 1 : 0).Sum();
        return new VPEntry(score, $"{score} victory points for a navy in {CountryState.ForEnum(countries[0]).Label} and army in {CountryState.ForEnum(countries[1]).Label}.");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}