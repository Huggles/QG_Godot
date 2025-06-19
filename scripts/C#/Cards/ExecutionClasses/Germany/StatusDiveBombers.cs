using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusDiveBombers : StatusCardLogic
{
    public override bool CanReactTo(ChangeEvent changeEvent) 
    {
        return base.CanReactTo(changeEvent) && changeEvent is BattleCountryChangeEvent battleCountryChangeEvent && battleCountryChangeEvent.TriggeringFaction == Faction && battleTargets.Count > 0;
    }
    
    public List<BattleTarget> battleTargets
    {
        get
        {      
            List<BattleTarget> battleTargets = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
                .Map(changeEvent => changeEvent.CountryId)
                .SelectMany(countryId => CountryState.ForId(countryId).AdjacentBattleTargets(Faction, CountryType.LAND)).Distinct().ToList();
            return battleTargets;
        }        
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                BattleTarget target = await new SelectBattleTargetHandler(battleTargets).Handle();
                BattleCountryChangeEvent battleCountryChangeEvent = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChangeEvent.IsTrigger = true;
                CardPlayPool.DoChangeEvent(battleCountryChangeEvent);
            })
        };
    }
}
