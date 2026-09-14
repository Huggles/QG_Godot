using System;
using System.Collections.Generic;
using Godot;

public partial class EventItalianEastAfrica : EventCardLogic
{
    public List<Country> targetCountries = [Country.NorthAfrica, Country.BayOfBengal];

    /// <summary>The army space and the navy space, in step order.</summary>
    public override TargetSet Targets() => TargetSet.Countries(targetCountries);
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)targetCountries[0]]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD);
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsBuildable([(int)targetCountries[0]], Faction),this))
            .WithGuidance($"Build an army in {CountryState.ForEnum(targetCountries[0]).Label}"),
            new ResultStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)targetCountries[1]]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD);
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsBuildable([(int)targetCountries[1]], Faction),this))
            .WithGuidance($"Build a navy in {CountryState.ForEnum(targetCountries[1]).Label}"),
        };
        
    }
}