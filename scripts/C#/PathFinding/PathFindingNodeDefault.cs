using Godot;
using System;
using System.Collections.Generic;

public partial class PathFindingNodeDefault : IPathFindingNode
{
    public List<int> ConnectedCountries(Faction faction)
    {
        throw new NotImplementedException();
    }

    public bool CountryLinksSupplyForFaction(int countryId, Faction faction)
    {
        return CountryState.ForId(countryId).OccupyingFactions.Contains(faction);
    }
}
