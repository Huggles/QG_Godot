using System;
using System.Collections.Generic;
using Godot;

public partial class ResponseRomanianReinforcements : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.UnitAboutToBeRemoved(Faction.GERMANY, UnitType.ARMY, requireInSupply: true), this),
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                if (CardPlayPool.LastNoneNewCardChangeEvent is RemoveUnitChangeEvent removeEvent) {
                    int countryId = removeEvent.CountryId;
                    DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT));
                    deployEvent.IsTrigger = true;
                    return deployEvent;
                }
                return null;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => {
                if (CardPlayPool.LastNoneNewCardChangeEvent is RemoveUnitChangeEvent removeEvent)
                    return CountryState.ForId(removeEvent.CountryId).Tags.Has(Tag.Recruitable, Faction);
                return false;
            }), this))
            .WithGuidance("Recruit an Italian Army in the space where the German Army was removed")
        };
    }
}