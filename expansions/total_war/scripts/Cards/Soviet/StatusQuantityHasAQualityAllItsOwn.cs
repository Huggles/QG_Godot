using System.Collections.Generic;

public partial class StatusQuantityHasAQualityAllItsOwn : StatusCardLogic
{
    private bool CanUse(BoardState board) =>
        TotalWarCards.FirstInHand(board, Faction, CardType.BUILD_ARMY) >= 0 && board.BuildableLand(Faction).Count > 0;

    public override TargetSet Targets() => TargetSet.Countries(BoardState.Live.BuildableLand(Faction));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.HasDeployedArmy(Faction).Immediately(), this),
        Condition.Build(new Condition.CustomCondition(s => CanUse(s.Board)), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(c => new DiscardHandCardsChangeEvent(Faction, Faction,
                new List<int> { TotalWarCards.FirstInHand(c.Board, Faction, CardType.BUILD_ARMY) })))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => CanUse(s.Board)), this))
            .WithGuidance("Discard a Build Army card to build an additional Army"),

        new ResultStep(this, Choose.CountryFrom(c => c.Board.BuildableLandIds(Faction),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .RequiringPreviousStep()
    };
}
