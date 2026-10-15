using System.Collections.Generic;
using System.Linq;

public partial class EWJapaneseFleetBlockadesVladivostok : EWCardLogic
{
    /// <summary>The Japanese Navy and Air Force in the Sea of Japan; each scores 1 VP and costs the Soviet Union a card.</summary>
    private List<UnitState> Blockaders(BoardState board) =>
        TotalWarCards.Pieces(board, Faction).Where(unit => !unit.IsArmy && board.CountryOf(unit) == (int)Country.SeaOfJapan).ToList();

    private Condition AnyBlockader() => Condition.Build(new Condition.CustomCondition(s => Blockaders(s.Board).Count > 0), this);

    public override TargetSet Targets() => TargetSet.Units(Blockaders(BoardState.Live));

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c => new ScorePointsChangeEvent(VPEntry.ForUnits(Blockaders(c.Board), 1, "Japanese Navy and Air Force in the Sea of Japan"), Faction)))
            .WithCondition(AnyBlockader)
            .WithGuidance("Gain 1 Victory Point for each Japanese Navy and Air Force in the Sea of Japan"),

        new ResultStep(this, Choose.Fixed(c => new ForceDiscardCardsChangeEvent(Faction, Faction.SOVIET, Blockaders(c.Board).Count)))
            .WithCondition(AnyBlockader)
            .WithGuidance("Make the Soviet Union discard 1 card for each")
    };
}
