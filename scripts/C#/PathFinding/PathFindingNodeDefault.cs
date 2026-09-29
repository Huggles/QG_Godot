using Godot;
using System;
using System.Collections.Generic;

public partial class PathFindingNodeDefault : IPathFindingNode
{
    public List<int> ConnectedCountries(Faction faction)
    {
        throw new NotImplementedException();
    }

    public bool CountryLinksSupplyForFaction(BoardState board, int countryId, Faction faction)
    {
        return board.UnitsIn(CountryState.ForId(countryId)).ContainsKey(faction);
    }
}
