using Godot;
using System;
using System.Collections.Generic;

public partial class AttackOption : GodotObject
{
    public int AttackingUnit;
    public Faction Faction;
    public List<int> AttackableUnits = new List<int>();
    public List<int> AttackableCountries = new List<int>();

    public AttackOption(int attackingUnit)
    {
        this.AttackingUnit = attackingUnit;
    }

    public static AttackOption CalculateAttackOptions(int unitId)
    {
        AttackOption attackOption = new AttackOption(unitId);
        UnitState unitState = UnitState.ForId(unitId);
        attackOption.Faction = unitState.Faction;        
        FactionTeam enemyTeam = StaticGameData.OpponentFactionTeamForFaction(unitState.Faction);
        foreach (CountryState countryState in unitState.CountryState.ConnectedCountries(unitState.Faction))
        {
            if (countryState.OccupyingTeam == enemyTeam)
            {
                foreach (int targetUnitId in countryState.Units.Values)
                {
                    attackOption.AttackableUnits.Add(targetUnitId);
                }
            }
            if (countryState.OccupyingTeam == FactionTeam.NONE)
            {
                attackOption.AttackableCountries.Add(countryState.Id);
            }
        }
        return attackOption;
    }
}
