using System.Collections.Generic;

public partial class StatusPeoplesLiberationArmy : StatusCardLogic, IUnitSupplyModifier
{
    private static readonly List<Country> recruitSpaces = [Country.Szechuan, Country.China, Country.Mongolia];

    public bool GrantsSupply(BoardState board, UnitState unit) => unit.Faction == Faction.CHINA && unit.IsArmy;

    private static bool CanRecruit(BoardState board) =>
        TotalWarCards.DeployTargets(board, Faction.CHINA, DeployType.RECRUIT, UnitType.ARMY, recruitSpaces).Count > 0;

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction.CHINA, DeployType.RECRUIT, UnitType.ARMY, recruitSpaces));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsPlayCardStep(), this),
        Condition.Build(new Condition.IsFactionTurn(Faction), this),
        Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this),
        Condition.Build(new Condition.CustomCondition(s => CanRecruit(s.Board)), this)
    };

    /// <summary>Instead of the hand play: spend the play, then recruit the Chinese Army. The Soviet player picks the space.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(_ => new SpendPlayActionChangeEvent(Faction)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => CanRecruit(s.Board)), this))
            .WithGuidance("Recruit a Chinese Army in Szechuan, China, or Mongolia instead of playing a card"),

        TotalWarCards.DeployStepFor(this, Faction.CHINA, DeployType.RECRUIT, UnitType.ARMY, recruitSpaces, "Recruit a Chinese Army")
            .RequiringPreviousStep()
    };
}
