using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BuildNavy : CardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                var targetableCountries = CountryState.BuildableSea(Faction);                
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, targetableCountries.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.HasBuildableSea(Faction), this))
            .WithGuidance("Build a navy")
        }; 
    }
}
