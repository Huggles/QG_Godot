using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusMareNostrum : StatusCardLogic, IStatusVictoryPoints
{
    public virtual VPEntry AddVictoryPoints()
    {
        List<Country> countries = [Country.NorthAfrica, Country.Africa, Country.MiddleEast];
        List<Faction> factions = [Faction];
        int score = factions.Sum((faction) => FactionState.ForEnum(faction).ActiveUnitIds.ToUnitStates().Map(unitState => countries.Contains(unitState.CountryState.Country) ? 1 : 0).Sum());        
        return new VPEntry(score, $"{score} victory points for all {FactionState.ForEnum(Faction).FactionData.FactionAdjactiveLabel} navies on the board.");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsGameFlowStep(TurnStep.VICTORY_POINT), this) };
    }
}