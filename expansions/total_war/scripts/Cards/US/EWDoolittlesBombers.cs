using System.Collections.Generic;
using System.Linq;

public partial class EWDoolittlesBombers : EWCardLogic
{
    /// <summary>The supplied US Navies within 3 spaces of Japan: any one of them lets the card be used.</summary>
    private List<UnitState> Navies(BoardState board) =>
        TotalWarCards.Within(board, board.ActiveUnits(Faction).Where(unit => unit.IsNavy && board.InSupply(unit)), Country.Japan, 3);

    private Condition InRange() => Condition.Build(new Condition.CustomCondition(s => Navies(s.Board).Count > 0), this);

    public override TargetSet Targets() =>
        TargetSet.Units(Navies(BoardState.Live)).Plus(TargetSet.Countries(new List<Country> { Country.Japan }));

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction.JAPAN, 1)))
            .WithCondition(InRange)
            .WithGuidance("Make Japan discard the top card of its draw deck"),

        new ResultStep(this, Choose.Fixed(_ => new ScorePointsChangeEvent(new VPEntry(3, "Doolittle's Bombers"), Faction)))
            .WithCondition(InRange)
            .WithGuidance("Gain 3 Victory Points")
    };
}
