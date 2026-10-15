using System.Collections.Generic;
using System.Linq;

public partial class StatusIrredentismoItaliano : StatusCardLogic, IVPModifier
{
    private List<UnitState> ScoringUnits(BoardState board) =>
        board.ActiveUnits(Faction).Where(unit => unit.IsArmy && board.CountryOf(unit) == (int)Country.WesternEurope).ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits(BoardState.Live));

    public VPEntry AddVictoryPoints(BoardState board) => VPEntry.ForUnits(ScoringUnits(board), 1, "an Army in Western Europe");

    protected override List<Condition> CardTriggers() => new() { Condition.Build(new Condition.IsVictoryPointStep(), this) };
}
