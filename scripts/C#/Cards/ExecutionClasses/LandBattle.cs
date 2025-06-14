using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class LandBattle : CardLogic
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
        return attackState.HasTargets;
    }
    
    public override async void InitialPlayStep()
    {
        DebugUtilities.PrintPeer(attackState.TargetUnitIds);
        int selectedUnitId = await new SelectUnitHandler(attackState.TargetUnitIds).Handle();

        BattleUnitChangeEvent battleUnitChangeEvent = BuildChangeEvent(new BattleUnitChangeEvent(Faction, selectedUnitId));
        battleUnitChangeEvent.IsTrigger = true;
        CardPlayPool.DoChangeEvent(battleUnitChangeEvent);
    }
}
