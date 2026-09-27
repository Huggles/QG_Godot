using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventForcedConscription : EventCardLogic
{
    private List<int> RecruitableCountryIds =>
        new List<int> { (int)Country.Germany }
            .Concat(CountryState.ForEnum(Country.Germany).ConnectedCountryStates
                .Select(cs => cs.Id))
            .Where(id => CountryState.ForId(id).Tags.Has(Tag.Recruitable, Faction))
            .Where(id => CountryState.ForId(id).Tags.Has(Tag.LandCountry, Faction.ALL))
            .ToList();

    /// <summary>Both recruits draw from the same list the steps select from.</summary>
    public override TargetSet Targets() => TargetSet.Countries(RecruitableCountryIds);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.CountryFrom(() => RecruitableCountryIds,
                countryId => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => RecruitableCountryIds.Count > 0), this))
            .WithGuidance("Recruit an Army in or adjacent to Germany"),
            new ResultStep(this, Choose.CountryFrom(() => RecruitableCountryIds,
                countryId => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => RecruitableCountryIds.Count > 0), this))
            .WithGuidance("Recruit a second Army in or adjacent to Germany"),
        };
    }
}