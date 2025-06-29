using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusGreaterEastAsiaCoProsperitySphere : StatusCardLogic
{
    public virtual VPEntry AddVictoryPoints()
    {
        List<Country> countries = [Country.Indonesia, Country.NewGuinea, Country.SouthEastAsia];
        List<Faction> factions = [Faction];
        int score = factions.Sum((faction) => FactionState.ForEnum(faction).ActiveUnitIds.ToUnitStates().Map(unitState => countries.Contains(unitState.CountryState.Country) ? 1 : 0).Sum());        
        return new VPEntry(score, $"{score} victory points for {FactionState.ForEnum(Faction).FactionData.FactionAdjactiveLabel} armies in {CountryState.ForEnum(countries[0]).Label},  {CountryState.ForEnum(countries[1]).Label} and {CountryState.ForEnum(countries[2]).Label}.");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsGameFlowStep(TurnStep.VICTORY_POINT), this) };
    }
}