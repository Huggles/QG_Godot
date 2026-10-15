using System.Collections.Generic;
using System.Linq;

public partial class ResponseChineseCivilWar : ResponseCardLogic
{
    public override bool IsFreePlayStepActivation => true;

    private static List<int> ChineseArmies(BoardState board) =>
        board.ActiveUnits(Faction.CHINA).Where(unit => unit.IsArmy && !board.ImmuneForTurn(unit)).Select(unit => unit.Id).ToList();

    public override TargetSet Targets() => TargetSet.Units(ChineseArmies(BoardState.Live));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtPlayStepStart(this);

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.UnitFrom(c => ChineseArmies(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => ChineseArmies(s.Board).Count > 0), this))
            .WithGuidance("Eliminate a Chinese Army")
    };
}
