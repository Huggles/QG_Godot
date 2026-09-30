using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventForcedConscription : EventCardLogic
{
    private List<int> RecruitableCountryIds(BoardState board) =>
        new List<int> { (int)Country.Germany }
            .Concat(CountryState.ForEnum(Country.Germany).ConnectedCountryStates
                .Select(cs => cs.Id))
            .Where(id => board.Of(CountryState.ForId(id)).Tags.Has(Tag.Recruitable, Faction))
            .Where(id => board.Of(CountryState.ForId(id)).Tags.Has(Tag.LandCountry, Faction.ALL))
            .ToList();

    /// <summary>Both recruits draw from the same list the steps select from.</summary>
    public override TargetSet Targets() => TargetSet.Countries(RecruitableCountryIds(BoardState.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.CountryFrom(c => RecruitableCountryIds(c.Board),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => RecruitableCountryIds(s.Board).Count > 0), this))
            .WithGuidance("Recruit an Army in or adjacent to Germany"),
            new ResultStep(this, Choose.CountryFrom(c => RecruitableCountryIds(c.Board),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => RecruitableCountryIds(s.Board).Count > 0), this))
            .WithGuidance("Recruit a second Army in or adjacent to Germany"),
        };
    }
}