using System.Collections.Generic;
using System.Linq;

public partial class StatusHokushinRon : StatusCardLogic, IVPModifier
{
    private static readonly List<int> scoringSpaces = [(int)Country.Vladivostok, (int)Country.Siberia];

    private List<UnitState> ScoringUnits(BoardState board) =>
        board.ActiveUnits(Faction).Where(unit => unit.IsArmy && scoringSpaces.Contains(board.CountryOf(unit))).ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits(BoardState.Live));

    public VPEntry AddVictoryPoints(BoardState board) => VPEntry.ForUnits(ScoringUnits(board), 1, "Armies in Vladivostok and Siberia");

    protected override List<Condition> CardTriggers() => new() { Condition.Build(new Condition.IsVictoryPointStep(), this) };
}
