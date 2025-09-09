using System;
using System.Collections.Generic;
using Godot;

public partial class EventMilitaryDictatorshipsInTheBalkans : EventCardLogic
{
    public List<Country> targetCountries = [Country.Ukraine, Country.Russia];
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler([targetCountries[0]]).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsBuildable(targetCountries[0], Faction),this))
            .WithGuidance($"Build an army in {CountryState.ForEnum(targetCountries[0]).Label}"),
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler([targetCountries[1]]).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsBuildable(targetCountries[1], Faction),this))
            .WithGuidance($"Build a navy in {CountryState.ForEnum(targetCountries[1]).Label}"),
        };
        
    }
}