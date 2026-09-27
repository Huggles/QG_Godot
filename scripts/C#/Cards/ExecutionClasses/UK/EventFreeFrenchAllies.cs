using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventFreeFrenchAllies : EventCardLogic
{
    private static readonly List<Country> targetCountries = [Country.WesternEurope, Country.NorthAfrica, Country.Africa];

    /// <summary>The spaces the recruit is actually available in right now — the same filtered list
    /// the step offers, so the preview and the offer cannot disagree.</summary>
    private List<CountryState> RecruitTargets =>
        CountryState.RecruitableLand(Faction).Where(cs => targetCountries.Contains(cs.Country)).ToList();

    public override TargetSet Targets() => TargetSet.Countries(RecruitTargets);

    public override List<CardStep> OnActivate()
    {

        return new List<CardStep> {
            new ResultStep(this, Choose.CountryFrom(() => RecruitTargets.ToCountryIds(),
                countryId => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                return RecruitTargets.Count > 0;
            }), this))
            .WithGuidance("Recruit an army in Western Europe, North Africa, or Africa"),
        };
    }
}
