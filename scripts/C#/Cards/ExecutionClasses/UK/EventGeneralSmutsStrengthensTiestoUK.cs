using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventGeneralSmutsStrengthensTiestoUK : EventCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Recruit an Army in Africa
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)Country.Africa]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Africa], Faction), this))
            .WithGuidance("Recruit an army in Africa"),
            
            // Recruit a Navy in Southern Ocean or Bay of Bengal
            new CardStep(this, async() => {
                List<Country> targetCountries = [Country.SouthernOcean, Country.BayOfBengal];
                var recruitableTargets = CountryState.RecruitableSea(Faction)
                    .Where(cs => targetCountries.Contains(cs.Country))
                    .ToList();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, recruitableTargets.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                List<Country> targetCountries = [Country.SouthernOcean, Country.BayOfBengal];
                return CountryState.RecruitableSea(Faction).Any(cs => targetCountries.Contains(cs.Country));
            }), this))
            .WithGuidance("Recruit a navy in the Southern Ocean or Bay of Bengal"),
        }; 
    }
}