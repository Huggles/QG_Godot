using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PathFindingNodeDefault : IPathFindingNode
{
    private readonly List<Faction> _linkFactions;

    /// <summary>A supply link is a space holding a piece of any of <paramref name="linkFactions"/> (BoardState.SupplyLinkFactions).</summary>
    public PathFindingNodeDefault(List<Faction> linkFactions)
    {
        _linkFactions = linkFactions;
    }

    public List<int> ConnectedCountries(Faction faction)
    {
        throw new NotImplementedException();
    }

    public bool CountryLinksSupplyForFaction(BoardState board, int countryId, Faction faction)
    {
        return _linkFactions.Any(board.UnitsIn(CountryState.ForId(countryId)).ContainsKey);
    }
}
