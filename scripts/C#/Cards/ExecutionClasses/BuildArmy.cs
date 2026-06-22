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
                InputRequest responseDto = await NetworkApi.Instance.SendInputRequest(new InputRequest.SelectCountryRequestHandler(Faction, targetableCountries.ToCountryIds()));                               
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, responseDto.ResponseCountryIds[0], DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;                
            })
            .WithCondition(()=> Condition.Build(new Condition.HasBuildableLand(Faction), this))            
            .WithGuidance("Build an army")
        }; 
    }
}
