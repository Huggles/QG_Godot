using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

public static class ListIdExtension
{
    public static List<UnitState> ToUnitStates(this List<int> list)
    {
        return UnitState.ForIds(list);
    }
    public static List<CountryState> ToCountryStates(this List<int> list)
    {
        return CountryState.ForIds(list);
    }

    public static List<int> ToUnitIds(this List<UnitState> unitStates)
    {        
        return unitStates.Map(unitState => unitState.Id);
    }
    
    public static List<int> ToCountryIds(this List<CountryState> countryStates)
    {
        return countryStates.Map(countryState => countryState.Id);
    }

    public static List<int> ToUnitIds(this List<CountryState> countryStates)
    {
        HashSet<int> unitIds = new HashSet<int>();
        foreach (CountryState countryState in countryStates)
        {
            foreach (int unitId in countryState.Units.Values)
            {
                unitIds.Add(unitId);
            }            
        }
        return unitIds.ToList();
    }

    public static List<U> Map<T, U>(this List<T> array, Func<T, U> predicate)
    {
        List<U> response = new List<U>();
        foreach (T item in array)
        {
            U mapped = predicate(item);
            response.Add(mapped);
        }
        return response;
    }
}
