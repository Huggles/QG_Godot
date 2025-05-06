using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class GameStateUtilities
{
    private static GameState GameState => GameSession.Instance.GameState;

    public static List<int> ActiveUnitsForFaction(Faction faction)
    {
        return GameState.FactionStateForEnum(faction).ActiveUnitIds;
    }

    public static List<int> SuppliedUnitsForFaction(Faction faction)
    {
        return GameState.FactionStateForEnum(faction).SuppliedUnitIds;
    }

    public static List<int> GetSupplyCountryIds(Faction faction)
    {
        var response = new List<int>();
        foreach (var countryState in GameState.CountryStates)
        {
            if (countryState.IsSupply && countryState.OccupyingFactions.Contains(faction))
            {
                response.Insert(0, countryState.Id); // push_front equivalent
            }
        }
        return response;
    }

    public static List<int> ActiveUnitIds(Faction faction)
    {
        var response = new List<int>();
        foreach (UnitState unitState in GameState.UnitStates)
        {
            if (unitState.Faction == faction && unitState.CountryId >= 0)
            {
                response.Add(unitState.Id);
            }
        }
        return response;
    }

    public static List<int> OccupiedCountryIds(Faction faction)
    {
        var response = new List<int>();
        foreach (var unitId in ActiveUnitIds(faction))
        {
            response.Add(UnitState.ForId(unitId).CountryId);
        }
        return response;
    }

    public static List<int> SuppliedUnitIds(Faction faction)
    {
        var response = new List<int>();
        foreach (var unitId in ActiveUnitIds(faction))
        {
            var unitState = UnitState.ForId(unitId);
            if (unitState.InSupply)
            {
                response.Add(unitId);
            }
        }
        return response;
    }

    public static List<int> UnsuppliedUnitIds(Faction faction)
    {
        var response = new List<int>();
        foreach (var unitId in ActiveUnitIds(faction))
        {
            var unitState = UnitState.ForId(unitId);
            if (!unitState.InSupply)
            {
                response.Add(unitId);
            }
        }
        return response;
    }

    public static StraightState StraightStateForNeighbors(int countryId1, int countryId2)
    {
        var matches = GameState.StraightStates.Where(straight =>
            (straight.ControlledCountryId1 == countryId1 && straight.ControlledCountryId2 == countryId2) ||
            (straight.ControlledCountryId1 == countryId2 && straight.ControlledCountryId2 == countryId1)).ToList();

        return matches.Count > 0 ? matches[0] : null;
    }

    public static void RequestResponseCardActivation(ChangeEvent changeEvent, Callable callback)
    {
        foreach (Faction faction in Enum.GetValues<Faction>()) // Assuming GetKeys() exists
        {
            RequestResponseCardActivationForFaction(changeEvent, (Faction)faction, callback);
        }
    }

    public static void RequestResponseCardActivationForFaction(ChangeEvent changeEvent, Faction faction, Callable callback)
    {
        if (DeckState.ForFaction(faction).ResponseCardIds.Count > 0)
        {
            // TODO: Implement callback invocation
        }
    }

    public static void RequestStatusCardActivation(ChangeEvent changeEvent, Callable callback)
    {
        foreach (Faction faction in Enum.GetValues<Faction>())
        {
            RequestResponseCardActivationForFaction(changeEvent, faction, callback);
        }
    }
}
