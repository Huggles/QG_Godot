using System;
using System.Collections.Generic;
using Godot;

public partial class EventGermanAidinGreece : EWCardLogic
{
    public List<Country> targetCountries = [Country.Balkans];
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler([targetCountries[0]]).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryHasEnemyUnit((int)targetCountries[0], Faction),this))
            .WithGuidance($"Eliminate an army in {CountryState.ForEnum(targetCountries[0]).Label}"),
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler([targetCountries[0]]).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsBuildable([(int)targetCountries[0]], Faction),this))
            .WithGuidance($"Build an army in {CountryState.ForEnum(targetCountries[0]).Label}"),
        };
        
    }
}