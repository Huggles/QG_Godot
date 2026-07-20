using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusBlitzkrieg : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.FactionBattled(Faction), this).Immediately(),
            // The space must be empty after the battle: if an enemy unit survived it is still
            // occupied and we cannot deploy an Army into it, so Blitzkrieg does not fire.
            Condition.Build(new Condition.CustomCondition(() => {
                var trigger = CardPlayPool.CurrentReactionTrigger as BattleCountryChangeEvent;
                return trigger != null && trigger.CountryState.Units.Count == 0;
            }), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 1));
                discardEvent.IsTrigger = false;
                await CardPlayPool.DoChangeEvent(discardEvent);

                var trigger = CardPlayPool.CurrentReactionTrigger as BattleCountryChangeEvent;
                if (trigger == null) return null;

                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, trigger.CountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;                
            }).WithGuidance("Deploy an army in the country where you just battled") 
        };
    }
}
