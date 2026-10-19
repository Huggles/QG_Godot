using System.Collections.Generic;
using System.Linq;

/// <summary>"Use during your Victory step. Gain +1 Victory Point per Italian Navy on the board." Offered as the step begins.</summary>
public partial class BolsterSupermarina : BolsterCardLogic
{
    private List<UnitState> Navies(BoardState board) => board.ActiveUnits(Faction).Where(unit => unit.IsNavy).ToList();

    public override TargetSet Targets() => TargetSet.Units(Navies(BoardState.Live));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.VICTORY_POINT, s => Navies(s.Board).Count > 0);

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c => new ScorePointsChangeEvent(new VPEntry(Navies(c.Board).Count, "Italian Navies (Supermarina)"), Faction)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => Navies(s.Board).Count > 0), this))
            .WithGuidance("Gain 1 Victory Point for each Italian Navy")
    };
}
