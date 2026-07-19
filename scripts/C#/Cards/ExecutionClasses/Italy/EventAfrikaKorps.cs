using System;
using System.Collections.Generic;
using Godot;

public partial class EventAfrikaKorps : ResponseCardLogic
{
    public List<Country> targetCountries = [Country.NorthAfrica, Country.MediterraneanSea];
    public Faction targetFaction = Faction.GERMANY;

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)targetCountries[0]]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(targetFaction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)targetCountries[0]], targetFaction),this))
            .WithGuidance($"Recruit a {FactionState.ForEnum(targetFaction).FactionData.FactionAdjactiveLabel} army in {CountryState.ForEnum(targetCountries[0]).Label}"),
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)targetCountries[1]]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(targetFaction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)targetCountries[1]], targetFaction),this))
            .WithGuidance($"Recruit a {FactionState.ForEnum(targetFaction).FactionData.FactionAdjactiveLabel} navy in {CountryState.ForEnum(targetCountries[1]).Label}"),
        };

    }
}