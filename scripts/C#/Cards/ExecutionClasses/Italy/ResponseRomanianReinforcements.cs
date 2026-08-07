using System;
using System.Collections.Generic;
using Godot;

public partial class ResponseRomanianReinforcements : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.CustomCondition(() => {
                if (CardPlayPool.CurrentReactionTrigger is RemoveUnitChangeEvent removeEvent)
                    return removeEvent.UnitState.Faction == Faction.GERMANY
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
                    DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT));
                    deployEvent.IsTrigger = true;
                    return deployEvent;
                }
                return null;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => {
                if (CardPlayPool.CurrentReactionTrigger is RemoveUnitChangeEvent removeEvent)
                    return CountryState.ForId(removeEvent.CountryId).Tags.Has(Tag.Recruitable, Faction);
                return false;
            }), this))
            .WithGuidance("Recruit an Italian Army in the space where the German Army was removed")
        };
    }
}