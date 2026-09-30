using System.Threading.Tasks;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusConscription : StatusCardLogic
{
    public List<int> BuildableLandCountries(BoardState board)
    {
        return board.BuildableLand(Faction).ToCountryIds();
    }

    /// <summary>Where the army may be built — the same list the step offers.</summary>
    public override TargetSet Targets() => TargetSet.Countries(BuildableLandCountries(BoardState.Live));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this)
        };
    }

    public override List<CardStep> OnActivate() 
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(_ => new SpendPlayActionChangeEvent(Faction)))
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(s =>
                new Condition.CountryIsBuildable(BuildableLandCountries(s.Board), Faction).MeetCondition(s)),this))
            .WithGuidance("Build an army"),

            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 2)))
            .RequiringPreviousStep(),

            new ResultStep(this, Choose.CountryFrom(c => BuildableLandCountries(c.Board),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .RequiringPreviousStep()
        };
    }
}
