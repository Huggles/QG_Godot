using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusBlitzkrieg : StatusCardLogic
{
    /// <summary>The space just battled, which the new Army moves into. Chosen by the trigger:
    /// this card offers no selection at all.</summary>
    public override TargetSet Targets() => TriggerTargets();

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.FactionBattled(Faction), this).Immediately(),
            // The space must be one we can actually BUILD in after the battle, which is the question
            // GameAPI.DeployUnitToCountry will ask when the step runs.
            //
            // This used to test Units.Count == 0 — "is the space empty" — which is only half of it.
            // CanBuild is occupancy AND, off a home space, an adjacent SUPPLIED unit. A space could
            // therefore come up empty, fire Blitzkrieg, and then refuse the build for want of supply:
            // the faction paid the discard (a VP, on an empty deck) and the once-per-turn activation
            // and got no Army. Asking CanBuild means the card simply does not offer itself instead.
            Condition.Build(new Condition.CustomCondition(() => {
                var trigger = TriggerContextAs<BattleCountryChangeEvent>();
                return trigger != null && trigger.CountryState.CanBuild(Faction);
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

                var trigger = TriggerContextAs<BattleCountryChangeEvent>();
                if (trigger == null) return;

                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, trigger.CountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            }).WithGuidance("Deploy an army in the country where you just battled") 
        };
    }
}
