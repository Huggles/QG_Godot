
using System;
using System.Collections.Generic;

public interface IPathFindingNode
{
    bool CountryLinksSupplyForFaction(int countryId, Faction faction);
    List<int> ConnectedCountries(Faction faction);
}
