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

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, RecruitableCountryIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => RecruitableCountryIds.Count > 0), this))
            .WithGuidance("Recruit an Army in or adjacent to Germany"),
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, RecruitableCountryIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => RecruitableCountryIds.Count > 0), this))
            .WithGuidance("Recruit a second Army in or adjacent to Germany"),
        };
    }
}