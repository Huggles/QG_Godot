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
        CountryState countryState = CountryState.ForId(countryId);
        if(countryState.OccupyingFactions.Contains(faction)){
            return true;        
        }
        return false;
    }

}
