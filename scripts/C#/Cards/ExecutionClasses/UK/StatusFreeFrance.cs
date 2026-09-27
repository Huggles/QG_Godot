using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class StatusFreeFrance : StatusCardLogic
{
    private static readonly List<int> buildCountryIds = [(int)Country.WesternEurope];

    /// <summary>Western Europe, the one space this builds into.</summary>
    public override TargetSet Targets() => TargetSet.Countries(buildCountryIds);

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this),
            Condition.Build(new Condition.CountryIsBuildable(buildCountryIds, Faction), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(() => new SpendPlayActionChangeEvent(Faction)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsBuildable(buildCountryIds, Faction), this))
            .WithGuidance("Discard top 2 deck cards to build an Army in Western Europe"),

            new RequirementStep(this, Choose.Fixed(() => new ForceDiscardCardsChangeEvent(Faction, Faction, 2)))
            .RequiringPreviousStep(),

            new ResultStep(this, Choose.Fixed(() => new DeployUnitChangeEvent(Faction, buildCountryIds[0], DeployType.BUILD)))
            .RequiringPreviousStep()
        };
    }
}