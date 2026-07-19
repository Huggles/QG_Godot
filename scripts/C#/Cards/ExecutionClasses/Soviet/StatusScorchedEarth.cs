using System;
using System.Collections.Generic;
using Godot;

public partial class StatusScorchedEarth : StatusCardLogic, ISupplyBlockModifier
{
    public bool BlocksSupply(int countryId, Faction faction)
    {
        return countryId == (int)Country.Ukraine
            && StaticGameData.FactionTeamForFaction(faction) == FactionTeam.AXIS;
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}