using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusUnimpededMerchantShipping : StatusCardLogic
{
    public virtual VPEntry AddVictoryPoints()
    {
        List<Country> countries = [Country.Hawaii];
        List<Faction> factions = [Faction.UNITED_STATES];
        int score = factions.Sum((faction) => FactionState.ForEnum(faction).ActiveUnitIds.ToUnitStates().Map(unitState => countries.Contains(unitState.CountryState.Country) ? 0 : 1).Sum());
        return new VPEntry(score, $"{score} victory points for no {FactionState.ForEnum(Faction).FactionData.FactionAdjactiveLabel} army in {CountryState.ForEnum(countries[0]).Label}.");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsGameFlowStep(TurnStep.VICTORY_POINT), this) };
    }
}