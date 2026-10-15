using System.Collections.Generic;

public partial class StatusSenegaleseTirailleurs : StatusCardLogic, IUnitSupplyModifier
{
    private static readonly List<Country> africa = [Country.Africa];

    /// <summary>Allied Armies in Africa, French ones included since the card names France.</summary>
    public bool GrantsSupply(BoardState board, UnitState unit) =>
        unit.IsArmy
        && board.CountryOf(unit) == (int)Country.Africa
        && (unit.Faction == Faction.FRANCE || StaticGameData.FactionsForTeam(FactionTeam.ALLIES).Contains(unit.Faction));

    private static bool CanRecruit(BoardState board) =>
        TotalWarCards.DeployTargets(board, Faction.FRANCE, DeployType.RECRUIT, UnitType.ARMY, africa).Count > 0;

    public override TargetSet Targets() => TargetSet.Countries(africa);

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsPlayCardStep(), this),
        Condition.Build(new Condition.IsFactionTurn(Faction), this),
        Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this),
        Condition.Build(new Condition.CustomCondition(s => CanRecruit(s.Board)), this)
    };

    /// <summary>Instead of the hand play: spend the play, then recruit the French Army.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(_ => new SpendPlayActionChangeEvent(Faction)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => CanRecruit(s.Board)), this))
            .WithGuidance("Recruit a French Army in Africa instead of playing a card"),

        TotalWarCards.DeployStepFor(this, Faction.FRANCE, DeployType.RECRUIT, UnitType.ARMY, africa, "Recruit a French Army in Africa")
            .RequiringPreviousStep()
    };
}
