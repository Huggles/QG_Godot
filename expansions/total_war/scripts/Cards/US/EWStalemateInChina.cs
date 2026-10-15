using System.Collections.Generic;
using System.Linq;

public partial class EWStalemateInChina : EWCardLogic
{
    /// <summary>The Chinese Armies and Air Forces on the board; each costs Japan 2 cards.</summary>
    private static List<UnitState> ChinesePieces(BoardState board) =>
        TotalWarCards.Pieces(board, Faction.CHINA).Where(unit => !unit.IsNavy).ToList();

    public override TargetSet Targets() => TargetSet.Units(ChinesePieces(BoardState.Live));

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c => new ForceDiscardCardsChangeEvent(Faction, Faction.JAPAN, 2 * ChinesePieces(c.Board).Count)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => ChinesePieces(s.Board).Count > 0), this))
            .WithGuidance("Make Japan discard 2 cards for each Chinese Army and Air Force")
    };
}
