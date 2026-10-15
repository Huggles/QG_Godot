using System.Collections.Generic;
using System.Linq;

public partial class EWOperationPike : EWCardLogic
{
    /// <summary>The UK Armies within 2 spaces of Ukraine. The "If" gates only the discard; the Victory Point always scores.</summary>
    private static List<UnitState> UkArmies(BoardState board) =>
        TotalWarCards.Within(board, board.ActiveUnits(Faction.UNITED_KINGDOM).Where(unit => unit.IsArmy), Country.Ukraine, 2);

    public override TargetSet Targets() => TargetSet.Units(UkArmies(BoardState.Live));

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction.SOVIET, 2)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => UkArmies(s.Board).Count > 0), this))
            .WithGuidance("Make the Soviet Union discard the top 2 cards of its draw deck"),

        new ResultStep(this, Choose.Fixed(_ => new ScorePointsChangeEvent(new VPEntry(1, "Operation Pike"), Faction)))
            .WithGuidance("Gain 1 Victory Point")
    };
}
