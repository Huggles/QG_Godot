using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusUnimpededMerchantShipping : StatusCardLogic, IVPModifier
{
    public virtual VPEntry AddVictoryPoints()
    {
        List<Country> countries = [Country.Hawaii];
        List<Faction> alliedFactions = [Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES];
        bool alliedArmyInHawaii = alliedFactions.Any(faction =>
            FactionState.ForEnum(faction).ActiveUnitIds.ToUnitStates()
                .Any(u => countries.Contains(u.CountryState.Country)));
        int score = alliedArmyInHawaii ? 0 : 1;
        return new VPEntry(score, $"{score} victory points for no Allied army in {CountryState.ForEnum(countries[0]).Label}.");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}