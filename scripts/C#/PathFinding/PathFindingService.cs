using Godot;
using System.Collections.Generic;

public partial class PathFindingService
{
    private AStar2D _aStar;

    public PathFindingService(IPathFindingNode nodeImplementation, Faction faction)
    {
        _aStar = new AStar2D();

        // Add eligible countries as A* nodes
        foreach (CountryState countryState in GameSession.Instance.GameState.CountryStates)
        {
            if (nodeImplementation.CountryLinksSupplyForFaction(countryState.Id, faction))
            {
                _aStar.AddPoint(countryState.Id, Vector2.One, 1);
            }
        }

        // Connect adjacent nodes
        foreach (int countryId in _aStar.GetPointIds())
        {
            CountryState countryState = CountryState.ForId(countryId);

            foreach (CountryState connectedState in countryState.ConnectedCountries(faction))
            {
                if (_aStar.HasPoint(connectedState.Id))
                {
                    _aStar.ConnectPoints(countryState.Id, connectedState.Id, bidirectional: true);
                }
            }
        }
    }

    public bool CalculatePath(int fromCountryId, int toCountryId)
    {
        long[] path = _aStar.GetIdPath(fromCountryId, toCountryId, false);

        if (path.Length == 0)
        {
            return false;
        }
        return true;
    }
}
