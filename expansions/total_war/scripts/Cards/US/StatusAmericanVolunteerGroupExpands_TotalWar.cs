using System.Collections.Generic;

public partial class StatusAmericanVolunteerGroupExpands_TotalWar : StatusCardLogic, IUnitSupplyModifier
{
    private static readonly List<int> szechuan = [(int)Country.Szechuan];

    /// <summary>Allied Armies in Szechuan, Chinese ones included since the card names China.</summary>
    public bool GrantsSupply(BoardState board, UnitState unit) =>
        unit.IsArmy
        && board.CountryOf(unit) == (int)Country.Szechuan
        && (unit.Faction == Faction.CHINA || StaticGameData.FactionsForTeam(FactionTeam.ALLIES).Contains(unit.Faction));

    public override TargetSet Targets() => TargetSet.Countries(szechuan);

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsPlayCardStep(), this),
        Condition.Build(new Condition.IsFactionTurn(Faction), this),
        Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this),
        Condition.Build(new Condition.CountryIsRecruitable(szechuan, Faction.CHINA), this)
    };

    /// <summary>Instead of the hand play: spend the play, then recruit the Chinese Army. No cost beyond the play.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(_ => new SpendPlayActionChangeEvent(Faction)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(szechuan, Faction.CHINA), this))
            .WithGuidance("Recruit a Chinese Army in Szechuan instead of playing a card"),

        new ResultStep(this, Choose.CountryFrom(_ => szechuan, (countryId, _) => new DeployUnitChangeEvent(Faction.CHINA, countryId, DeployType.RECRUIT)))
            .RequiringPreviousStep()
    };
}
