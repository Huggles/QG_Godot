using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusDiveBombers : StatusCardLogic
{   
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.FactionBattled(Faction), this).Immediately()
        };
    }
    
    public List<BattleTarget> battleTargets
    {
        get
        {
            var trigger = CardPlayPool.CurrentReactionTrigger as BattleCountryChangeEvent;
            if (trigger == null) return new List<BattleTarget>();
            return CountryState.ForId(trigger.CountryId).AdjacentBattleTargets(Faction, CountryType.LAND).Distinct().ToList();
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

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 1));
                discardEvent.IsTrigger = false;
                await discardEvent.ApplyChange();

                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, battleTargets).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChangeEvent = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChangeEvent.IsTrigger = true;
                return battleCountryChangeEvent;
            })
            .WithGuidance("Battle the same or an adjacent country where you've battle this turn")
            .WithCondition(()=>{
                return Condition.Build(
                    new Condition.CountryIsAttackable(battleTargetCountryIds, Faction), this); })
        };
    }
}
