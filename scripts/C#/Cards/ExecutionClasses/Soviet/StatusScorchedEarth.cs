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

    /// <summary>Ukraine, the space this denies the Axis as a supply source. The card grants no
    /// build and takes no piece; the space it turns off is the whole of its board effect.</summary>
    public override TargetSet Targets() => TargetSet.Countries(new List<Country> { Country.Ukraine });

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}