using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BuildArmy : CardLogic
{  
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                var targetableCountries = CountryState.BuildableLand(Faction);
                int selectedCountryId = await new SelectCountryHandler(targetableCountries.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;                
            })
            .WithCondition(()=> Condition.Build(new Condition.HasBuildableLand(Faction), this))            
            .WithGuidance("Build an army")
        }; 
    }
}
