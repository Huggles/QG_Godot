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
            new ResultStep(this, Choose.CountryFrom(_ => recruitCountryIds,
                (countryId, _) => new DeployUnitChangeEvent(targetFaction, countryId, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(recruitCountryIds, targetFaction), this))
            .WithGuidance("Recruit a Soviet Army in Russia"),
            new ResultStep(this, Choose.CountryFrom(c => c.Board.BuildableLand(targetFaction).ToCountryIds(),
                (countryId, _) => new DeployUnitChangeEvent(targetFaction, countryId, DeployType.BUILD)))
            .WithCondition(() => Condition.Build(new Condition.HasBuildableLand(targetFaction), this))
            .WithGuidance("Let the Soviet Union build an Army"),
        };
    }
}