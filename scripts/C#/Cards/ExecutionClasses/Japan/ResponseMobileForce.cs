using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseMobileForce : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { 
            Condition.Build(new Condition.IsStartStep(), this) 
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                var northPacific = CountryState.ForEnum(Country.NorthPacific);
                var recruitableTargets = CountryState.RecruitableSea(Faction)
                    .Where(cs => cs == northPacific || northPacific.ConnectedCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = await new SelectCountryHandler(recruitableTargets.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var northPacific = CountryState.ForEnum(Country.NorthPacific);
                return CountryState.RecruitableSea(Faction).Any(cs => cs == northPacific || northPacific.ConnectedCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Recruit a navy in or adjacent to the North Pacific"),
        }; 
    }
}