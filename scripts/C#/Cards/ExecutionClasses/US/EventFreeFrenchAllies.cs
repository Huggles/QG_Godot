using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventFreeFrenchAllies : EventCardLogic
{
    // A US card, but the recruit is a United Kingdom Army.
    private static readonly Faction targetFaction = Faction.UNITED_KINGDOM;
    private static readonly List<Country> targetCountries = [Country.WesternEurope, Country.NorthAfrica, Country.Africa];

    /// <summary>The spaces the recruit is actually available in right now — the same filtered list
    /// the step offers, so the preview and the offer cannot disagree.</summary>
    private List<CountryState> RecruitTargets(BoardState board) =>
        board.RecruitableLand(targetFaction).Where(cs => targetCountries.Contains(cs.Country)).ToList();

    public override TargetSet Targets() => TargetSet.Countries(RecruitTargets(BoardState.Live));

    public override List<CardStep> OnActivate()
    {

        return new List<CardStep> {
            new ResultStep(this, Choose.CountryFrom(c => RecruitTargets(c.Board).ToCountryIds(),
                (countryId, _) => new DeployUnitChangeEvent(targetFaction, countryId, DeployType.RECRUIT)))
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(s => {
                return RecruitTargets(s.Board).Count > 0;
            }), this))
            .WithGuidance("Let the United Kingdom recruit an Army in Western Europe, North Africa, or Africa"),
        };
    }
}
