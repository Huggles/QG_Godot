using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class SeaBattle : CardLogic
{
    public AttackState attackState
    { 
        get
        {           
            return AttackState.AttackStateForFaction(Faction);
        }
    }
    public override bool CanPlayCard()
    {
        return attackState.TargetsOfType(UnitType.NAVY).Count > 0;
    }
    
    public override async void InitialPlayStep()
    {
        List<int> navyUnits = attackState.TargetUnitIds.Where(unitId => UnitState.ForId(unitId).Type == UnitType.NAVY).ToList();
        int selectedUnitId = await new SelectUnitHandler(navyUnits).Handle();

        BattleUnitChangeEvent battleUnitChangeEvent = BuildChangeEvent(new BattleUnitChangeEvent(Faction, selectedUnitId));
        battleUnitChangeEvent.IsTrigger = true;
        CardPlayPool.DoChangeEvent(battleUnitChangeEvent);
    }
}
