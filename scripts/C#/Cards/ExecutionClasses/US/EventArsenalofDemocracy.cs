using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventArsenalofDemocracy : EventCardLogic
{
    private static readonly Faction targetFaction = Faction.UNITED_KINGDOM;

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                var buildableLand = CountryState.BuildableLand(targetFaction).ToCountryIds();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(targetFaction, buildableLand).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(targetFaction, selectedCountryId, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.HasBuildableLand(targetFaction), this))
            .WithGuidance("United Kingdom builds an Army"),
            new CardStep(this, async() => {
                var buildableSea = CountryState.BuildableSea(targetFaction).ToCountryIds();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(targetFaction, buildableSea).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(targetFaction, selectedCountryId, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.HasBuildableSea(targetFaction), this))
            .WithGuidance("United Kingdom builds a Navy"),
        };
    }
}