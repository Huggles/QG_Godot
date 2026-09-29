using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BuildNavy : CardLogic
{
    /// <summary>
    /// The one definition of what this card can hit, read by both the step's selection and
    /// <see cref="Targets"/> so the hover preview cannot drift from the real offer.
    /// </summary>
    private List<int> BuildTargets(BoardState board) => board.BuildableSea(Faction).ToCountryIds();

    public override TargetSet Targets() => TargetSet.Countries(BuildTargets(BoardState.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new ResultStep(this, Choose.CountryFrom(c => BuildTargets(c.Board),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .WithCondition(()=> Condition.Build(new Condition.HasBuildableSea(Faction), this))
            .WithAdvisoryConditions(()=> new List<Condition> {
                Condition.Build(new Condition.HasVacantBuildableSea(Faction), this),
                Condition.Build(new Condition.HasAvailableUnits(Faction, UnitType.NAVY), this)
            })
            .WithGuidance("Build a navy")
        }; 
    }
}
