using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BuildNavy : CardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                var targetableCountries = CountryState.BuildableSea(Faction);                
                Variant[] response = await NetworkApi.Instance.SendInputRequest(new InputRequest.SelectCountryRequestHandler(Faction, targetableCountries.ToCountryIds()));
                InputRequest responseDto = InputRequest.FromJson(response[0].AsString());                
                int selectedCountryId = responseDto.ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.HasBuildableSea(Faction), this))
            .WithGuidance("Build a navy")
        }; 
    }
}
