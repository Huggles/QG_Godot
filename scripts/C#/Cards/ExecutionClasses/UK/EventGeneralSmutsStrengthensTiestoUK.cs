using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventGeneralSmutsStrengthensTiestoUK : EventCardLogic
{
    private static readonly List<int> armyCountryIds = [(int)Country.Africa];
    private static readonly List<Country> navyCountries = [Country.SouthernOcean, Country.BayOfBengal];

    /// <summary>The sea spaces the navy may actually go to right now — the same filtered list the
    /// second step offers.</summary>
    private List<CountryState> NavyTargets(BoardState board) =>
        board.RecruitableSea(Faction).Where(cs => navyCountries.Contains(cs.Country)).ToList();

    /// <summary>Africa for the army, and whichever of the two sea spaces is open, for the navy.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(armyCountryIds).Plus(TargetSet.Countries(NavyTargets(BoardState.Live)));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Recruit an Army in Africa
            new ResultStep(this, Choose.CountryFrom(_ => armyCountryIds,
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable(armyCountryIds, Faction), this))
            .WithGuidance("Recruit an army in Africa"),
            
            // Recruit a Navy in Southern Ocean or Bay of Bengal
            new ResultStep(this, Choose.CountryFrom(c => NavyTargets(c.Board).ToCountryIds(),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(s => {
                return NavyTargets(s.Board).Count > 0;
            }), this))
            .WithGuidance("Recruit a navy in the Southern Ocean or Bay of Bengal"),
        }; 
    }
}