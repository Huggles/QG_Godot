using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventFreeFrenchAllies : EventCardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        List<Country> targetCountries = [Country.WesternEurope, Country.NorthAfrica, Country.Africa];

        return new List<CardStep> {
            new CardStep(this, async() => {
                var recruitableTargets = CountryState.RecruitableLand(Faction)
                    .Where(cs => targetCountries.Contains(cs.Country))
                    .ToList();
                int selectedCountryId = await new SelectCountryHandler(recruitableTargets.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                return CountryState.RecruitableLand(Faction).Any(cs => targetCountries.Contains(cs.Country));
            }), this))
            .WithGuidance("Recruit an army in Western Europe, North Africa, or Africa"),
        };
    }
}
