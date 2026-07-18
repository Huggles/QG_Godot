using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventMurmanskConvoy : EWCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction.SOVIET, [(int)Country.Russia]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction.SOVIET, selectedCountryId, DeployType.RECRUIT));
                deployEvent.IsTrigger = true;
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Russia], Faction.SOVIET), this))
            .WithGuidance("Recruit a Soviet Army in Russia"),
            new CardStep(this, async() => {
                var buildableLand = CountryState.BuildableLand(Faction.SOVIET).ToCountryIds();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction.SOVIET, buildableLand).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction.SOVIET, selectedCountryId, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.HasBuildableLand(Faction.SOVIET), this))
            .WithGuidance("Soviet Union may build an Army"),
        };
    }
}