using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

public class ConditionPrefabCanBuild : ConditionPrefab
{
    public override bool MeetCondition()
    {        
        Faction f = Parameters.FactionEnums.Count > 0 ? Faction.GERMANY : Parameters.FactionEnums[0];
        List<CountryState> countryStates = Parameters.CountryStates.Count > 0 ? Parameters.CountryStates : CountryState.AllCountryStates;
        return countryStates.Any(cs => cs.CanBuild(f));
    }

}
