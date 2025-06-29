using System;
using System.Collections.Generic;
using Godot;

public partial class EventAfrikaKorps : ResponseCardLogic
{
    public List<Country> targetCountries = [Country.NorthAfrica, Country.MediterraneanSea];
    public Faction targetFaction = Faction.GERMANY;

    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler([targetCountries[0]]).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(targetFaction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable((int)targetCountries[0], Faction),this))
            .WithGuidance($"Recruit a {FactionState.ForEnum(targetFaction).FactionData.FactionAdjactiveLabel} army in {CountryState.ForEnum(targetCountries[0]).Label}"),
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler([targetCountries[0]]).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(targetFaction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable(targetCountries[1], Faction),this))
            .WithGuidance($"Recruit a {FactionState.ForEnum(targetFaction).FactionData.FactionAdjactiveLabel} navy in {CountryState.ForEnum(targetCountries[1]).Label}"),
        };

    }
}