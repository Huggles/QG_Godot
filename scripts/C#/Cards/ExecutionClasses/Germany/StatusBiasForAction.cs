using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusBiasForAction : StatusCardLogic
{
    public override bool CanReactTo(ChangeEvent changeEvent) 
    {
        return base.CanReactTo(changeEvent) && changeEvent is DeployUnitChangeEvent deployUnitChangeEvent && deployUnitChangeEvent.TriggeringFaction == Faction.GERMANY && BattleTargets.Count > 0;
    }
    
    public List<BattleTarget> BattleTargets
    {
        get
        {            
            List<BattleTarget> battleTargets = CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()
                .Map(changeEvent => changeEvent.CountryId)
                .SelectMany(countryId => CountryState.ForId(countryId).AdjacentBattleTargets(Faction, CountryType.LAND)).Distinct().ToList();
            return battleTargets;
        }        
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                BattleTarget battleTarget = await new SelectBattleTargetHandler(BattleTargets).Handle();
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                CardPlayPool.DoChangeEvent(battleCountryChange);
            })
        };
    }
}
