using System;
using System.Collections.Generic;
using Godot;

public partial class ResponseGermanReinforcementsCounterattack : ResponseCardLogic
{
    /// <summary>The space the Italian Army was removed from, where the German replacement lands. The removal has already applied by the time this window opens, so the event reports the country and no longer the unit.</summary>
    public override TargetSet Targets() => TriggerTargets();

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
            new ResultStep(this, Choose.Fixed(() => {
                if (CardPlayPool.CurrentReactionTrigger is RemoveUnitChangeEvent removeEvent)
                    return new DeployUnitChangeEvent(Faction.GERMANY, removeEvent.CountryId, DeployType.RECRUIT);
                return null;
            }))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => {
                if (CardPlayPool.CurrentReactionTrigger is RemoveUnitChangeEvent removeEvent)
                    return CountryState.ForId(removeEvent.CountryId).Tags.Has(Tag.Recruitable, Faction.GERMANY);
                return false;
            }), this))
            .WithGuidance("Recruit a German Army in the space where the Italian Army was removed")
        };
    }
}