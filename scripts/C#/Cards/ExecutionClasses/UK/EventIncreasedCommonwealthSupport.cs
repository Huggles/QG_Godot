using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventIncreasedCommonwealthSupport : EventCardLogic
{
    public override List<CardStep> OnActivate()
    {
        List<Country> targetCountries = [Country.India, Country.Australia, Country.Canada];
        
        return new List<CardStep> {
            new CardStep(this, async() => {
                var recruitableTargets = CountryState.RecruitableLand(Faction)
                    .Where(cs => targetCountries.Contains(cs.Country))
                    .ToList();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, recruitableTargets.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                return CountryState.RecruitableLand(Faction).Any(cs => targetCountries.Contains(cs.Country));
            }), this))
            .WithGuidance("Recruit an army in India, Australia, or Canada"),
        }; 
    }
}