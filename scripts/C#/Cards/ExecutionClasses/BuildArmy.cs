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
                if(true)
                {
                    throw new Exception("BuildArmy is deprecated. Use BuildArmyWithInputRequest instead.");
                }
                
                var targetableCountries = CountryState.BuildableLand(Faction);
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, targetableCountries.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;                
            })
            .WithCondition(()=> Condition.Build(new Condition.HasBuildableLand(Faction), this))            
            .WithGuidance("Build an army")
        }; 
    }
}
