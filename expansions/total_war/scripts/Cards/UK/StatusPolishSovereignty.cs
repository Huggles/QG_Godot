using System.Collections.Generic;

public partial class StatusPolishSovereignty : StatusCardLogic, IUnitSupplyModifier
{
    private static readonly List<Country> easternEurope = [Country.EasternEurope];

    public bool GrantsSupply(BoardState board, UnitState unit) =>
        unit.Faction == Faction.UNITED_KINGDOM && unit.IsArmy && board.CountryOf(unit) == (int)Country.EasternEurope;

    private bool CanRecruit(BoardState board) =>
        TotalWarCards.DeployTargets(board, Faction, DeployType.RECRUIT, UnitType.ARMY, easternEurope).Count > 0;

    public override TargetSet Targets() => TargetSet.Countries(easternEurope);

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsPlayCardStep(), this),
        Condition.Build(new Condition.IsFactionTurn(Faction), this),
        Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this),
        Condition.Build(new Condition.CustomCondition(s => CanRecruit(s.Board)), this)
    };

    /// <summary>Instead of the hand play: spend the play, then recruit the Army.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(_ => new SpendPlayActionChangeEvent(Faction)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => CanRecruit(s.Board)), this))
            .WithGuidance("Recruit an Army in Eastern Europe instead of playing a card"),

        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, easternEurope, "Recruit an Army in Eastern Europe")
            .RequiringPreviousStep()
    };
}
