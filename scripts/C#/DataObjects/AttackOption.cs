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

    /// <summary>What the unit could battle from where it stands on <paramref name="board"/>: enemy units not immune, and empty neighbours.</summary>
    public static AttackOption CalculateAttackOptions(int unitId, BoardState board)
    {
        AttackOption attackOption = new AttackOption(unitId);
        UnitState unitState = UnitState.ForId(unitId);
        attackOption.Faction = unitState.Faction;        
        FactionTeam enemyTeam = StaticGameData.OpponentFactionTeamForFaction(unitState.Faction);
        CountryState from = CountryState.ForId(board.CountryOf(unitState));
        foreach (CountryState countryState in board.AdjacentCountryStates(unitState.Faction, from))
        {
            FactionTeam occupying = board.OccupyingTeam(countryState);
            if (occupying == enemyTeam)
            {
                foreach (int targetUnitId in board.UnitsIn(countryState).Values)
                {
                    UnitState targetUnitState = UnitState.ForId(targetUnitId);
                    if (board.ImmuneForTurn(targetUnitState) == false)
                    {
                        attackOption.AttackableUnits.Add(targetUnitId);
                    }
                }
            }
            if (occupying == FactionTeam.NONE)
            {
                attackOption.AttackableCountries.Add(countryState.Id);
            }
        }
        return attackOption;
    }
}
