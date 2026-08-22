using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BuildArmy : CardLogic
{  
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {         
                var targetableCountries = CountryState.BuildableLand(Faction);
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, targetableCountries.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.HasBuildableLand(Faction), this))
            .WithAdvisoryCondition(()=> Condition.Build(new Condition.HasVacantBuildableLand(Faction), this))
            .WithGuidance("Build an army")
        }; 
    }
}
