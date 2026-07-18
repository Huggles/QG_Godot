using Godot;
using System.Collections.Generic;

public partial class PathFindingService
{
    private AStar2D _aStar;

    public PathFindingService(IPathFindingNode nodeImplementation, Faction faction)
    {
        _aStar = new AStar2D();

        // Add eligible countries as A* nodes
        foreach (CountryState countryState in GameSession.Current.GameState.CountryStates)
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

            foreach (CountryState connectedState in countryState.AdjacentCountryStates(faction))
            {
                if (_aStar.HasPoint(connectedState.Id))
                {
                    _aStar.ConnectPoints(countryState.Id, connectedState.Id, bidirectional: true);
                }
            }
        }
    }

    /// <summary>Geographic (supply-agnostic) — includes all countries and all connections.</summary>
    public PathFindingService()
    {
        _aStar = new AStar2D();
        foreach (CountryState cs in GameSession.Current.GameState.CountryStates)
            _aStar.AddPoint(cs.Id, Vector2.One, 1);
        foreach (CountryState cs in GameSession.Current.GameState.CountryStates)
            foreach (CountryState neighbor in cs.ConnectedCountryStates)
                if (_aStar.HasPoint(neighbor.Id))
                    _aStar.ConnectPoints(cs.Id, neighbor.Id, bidirectional: true);
    }

    /// <summary>Returns the number of hops in the shortest path, or -1 if no path exists.</summary>
    public int CalculatePath(int fromCountryId, int toCountryId)
    {
        long[] path = _aStar.GetIdPath(fromCountryId, toCountryId, false);
        return path.Length == 0 ? -1 : path.Length - 1;
    }

    /// <summary>Returns true if the shortest geographic path between two countries is within maxHops steps (supply-agnostic).</summary>
    public static bool IsWithinGeographicDistance(int fromCountryId, int toCountryId, int maxHops)
    {
        var service = new PathFindingService();
        int distance = service.CalculatePath(fromCountryId, toCountryId);
        return distance >= 0 && distance <= maxHops;
    }
}
