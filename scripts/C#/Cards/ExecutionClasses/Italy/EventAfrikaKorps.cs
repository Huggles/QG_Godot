using System;
using System.Collections.Generic;
using Godot;

public partial class EventAfrikaKorps : EventCardLogic
{
    public List<Country> targetCountries = [Country.NorthAfrica, Country.MediterraneanSea];
    public Faction targetFaction = Faction.GERMANY;

    /// <summary>The two spaces the German army and navy land in.</summary>
    public override TargetSet Targets() => TargetSet.Countries(targetCountries);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.CountryFrom(_ => new List<int> { (int)targetCountries[0] },
                (countryId, _) => new DeployUnitChangeEvent(targetFaction, countryId, DeployType.RECRUIT)))
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)targetCountries[0]], targetFaction),this))
            .WithGuidance($"Recruit a {FactionState.ForEnum(targetFaction).FactionData.FactionAdjactiveLabel} army in {CountryState.ForEnum(targetCountries[0]).Label}"),
            new ResultStep(this, Choose.CountryFrom(_ => new List<int> { (int)targetCountries[1] },
                (countryId, _) => new DeployUnitChangeEvent(targetFaction, countryId, DeployType.RECRUIT)))
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)targetCountries[1]], targetFaction),this))
            .WithGuidance($"Recruit a {FactionState.ForEnum(targetFaction).FactionData.FactionAdjactiveLabel} navy in {CountryState.ForEnum(targetCountries[1]).Label}"),
        };

    }
}