using System;
using System.Collections.Generic;
using Godot;

public partial class ResponseRomanianReinforcements : ResponseCardLogic
{
    /// <summary>The space the German Army was removed from, where the Italian replacement lands. The removal has already applied by the time this window opens, so the event reports the country and no longer the unit.</summary>
    public override TargetSet Targets() => TriggerTargets();

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.CustomCondition(s => {
                if (s.ReactionTrigger is RemoveUnitChangeEvent removeEvent)
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
            new ResultStep(this, Choose.Fixed(c => {
                if (c.Situation.ReactionTrigger is RemoveUnitChangeEvent removeEvent)
                    return new DeployUnitChangeEvent(Faction, removeEvent.CountryId, DeployType.RECRUIT);
                return null;
            }))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => {
                if (s.ReactionTrigger is RemoveUnitChangeEvent removeEvent)
                    return s.Board.Of(CountryState.ForId(removeEvent.CountryId)).Tags.Has(Tag.Recruitable, Faction);
                return false;
            }), this))
            .WithGuidance("Recruit an Italian Army in the space where the German Army was removed")
        };
    }
}