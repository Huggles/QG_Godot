using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventMurmanskConvoy : EventCardLogic
{
    // Both steps act on the Soviet Union, not on this card's own faction.
    private static readonly Faction targetFaction = Faction.SOVIET;
    private static readonly List<int> recruitCountryIds = [(int)Country.Russia];

    /// <summary>Russia for the granted recruit, plus wherever the Soviets could then build.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(recruitCountryIds)
            .Plus(TargetSet.Countries(CountryState.BuildableLand(targetFaction)));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(targetFaction, recruitCountryIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(targetFaction, selectedCountryId, DeployType.RECRUIT));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(recruitCountryIds, targetFaction), this))
            .WithGuidance("Recruit a Soviet Army in Russia"),
            new CardStep(this, async() => {
                var buildableLand = CountryState.BuildableLand(targetFaction).ToCountryIds();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(targetFaction, buildableLand).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(targetFaction, selectedCountryId, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.HasBuildableLand(targetFaction), this))
            .WithGuidance("Soviet Union may build an Army"),
        };
    }
}