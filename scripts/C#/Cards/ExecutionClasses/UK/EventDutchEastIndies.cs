using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventDutchEastIndies : EventCardLogic
{
    /// <summary>The two army spaces and the navy space, in step order.</summary>
    private static readonly List<Country> targetCountries = [Country.Indonesia, Country.NewGuinea, Country.SouthChinaSea];

    public override TargetSet Targets() => TargetSet.Countries(targetCountries);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            RecruitStep(targetCountries[0], "Recruit an army in Indonesia"),
            RecruitStep(targetCountries[1], "Recruit an army in New Guinea"),
            RecruitStep(targetCountries[2], "Recruit a navy in the South China Sea"),
        };
    }

    private ResultStep RecruitStep(Country country, string guidance)
    {
        List<int> countryIds = [(int)country];
        return new ResultStep(this, Choose.CountryFrom(_ => countryIds,
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(countryIds, Faction), this))
            .WithGuidance(guidance);
    }
}
