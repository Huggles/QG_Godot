using Godot;
using System;

using System.Collections.Generic;
using System.Linq;

public class AttackState
{
    public Faction Faction;
    public List<int> TargetUnitIds = new List<int>();
    public List<int> TargetEmptyCountryIds = new List<int>(); 

    public bool HasTargets { get { return HasTargetUnits || HasTargetEmptyCountries; } }
    public bool HasTargetUnits { get { return TargetUnitIds.Count > 0; } }
    public bool HasTargetEmptyCountries { get { return TargetEmptyCountryIds.Count > 0; } }

    public static AttackState AttackStateForFaction(Faction faction)
    {
        List<int> suppliedUnitIds = GameStateUtilities.SuppliedUnitsForFaction(faction);
        AttackState attackState = new();
        attackState.Faction = faction;
        foreach (int suppliedUnitId in suppliedUnitIds)
        {
            attackState += AttackOption.CalculateAttackOptions(suppliedUnitId);
        }
        return attackState;
    }    

    public static AttackState operator +(AttackState attackState, AttackOption attackOption)
    {
        UnitState unitState = UnitState.ForId(attackOption.AttackingUnit);
        CountryState countryState = unitState.CountryState;
        foreach (int attackableUnit in attackOption.AttackableUnits)
        {
            attackState.TargetUnitIds.Add(attackableUnit);
        }
        foreach (int attackableCountry in attackOption.AttackableCountries)
        {
            attackState.TargetEmptyCountryIds.Add(attackableCountry);
        }
        return attackState;
    }    
}

