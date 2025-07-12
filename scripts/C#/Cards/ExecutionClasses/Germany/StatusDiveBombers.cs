using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusDiveBombers : StatusCardLogic
{   
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.FactionBattled(Faction), this)
        };
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
    public List<int> battleTargetCountryIds
    {
        get
        {
            List<int> ids = battleTargets.Where(bt => bt.Type == TargetType.COUNTRY).ToList().Map((bt) => bt.Id);
            return ids;
        }
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                BattleTarget target = await new SelectBattleTargetHandler(battleTargets).Handle();
                BattleCountryChangeEvent battleCountryChangeEvent = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChangeEvent.IsTrigger = true;
                _ = CardPlayPool.DoChangeEvent(battleCountryChangeEvent);
            })
            .WithGuidance("Battle the same or an adjacent country where you've battle this turn")
            .WithCondition(()=>{
                return Condition.Build(
                    new Condition.CountryIsAttackable(battleTargetCountryIds, Faction), this); })
        };
    }
}
