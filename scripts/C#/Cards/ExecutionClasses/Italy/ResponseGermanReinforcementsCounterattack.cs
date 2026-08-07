using System;
using System.Collections.Generic;
using Godot;

public partial class ResponseGermanReinforcementsCounterattack : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.CustomCondition(() => {
                if (CardPlayPool.CurrentReactionTrigger is RemoveUnitChangeEvent removeEvent)
                    return removeEvent.UnitState.Faction == Faction.ITALY
                        && removeEvent.UnitState.Type == UnitType.ARMY
                        && removeEvent.WasInSupply;
                return false;
            }).InReactionWindow(), this),
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                if (CardPlayPool.CurrentReactionTrigger is RemoveUnitChangeEvent removeEvent) {
                    int countryId = removeEvent.CountryId;
                    DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction.GERMANY, countryId, DeployType.RECRUIT));
                    deployEvent.IsTrigger = true;
                    return deployEvent;
                }
                return null;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => {
                if (CardPlayPool.CurrentReactionTrigger is RemoveUnitChangeEvent removeEvent)
                    return CountryState.ForId(removeEvent.CountryId).Tags.Has(Tag.Recruitable, Faction.GERMANY);
                return false;
            }), this))
            .WithGuidance("Recruit a German Army in the space where the Italian Army was removed")
        };
    }
}