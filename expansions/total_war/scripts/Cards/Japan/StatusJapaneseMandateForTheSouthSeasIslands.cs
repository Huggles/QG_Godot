using System.Collections.Generic;
using System.Linq;

public partial class StatusJapaneseMandateForTheSouthSeasIslands : StatusCardLogic, IVPModifier
{
    private List<UnitState> ScoringUnits(BoardState board) =>
        board.ActiveUnits(Faction).Where(unit => unit.IsNavy && board.CountryOf(unit) == (int)Country.CentralPacific).ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits(BoardState.Live));

    public VPEntry AddVictoryPoints(BoardState board) => VPEntry.ForUnits(ScoringUnits(board), 1, "a Navy in the Central Pacific");

    protected override List<Condition> CardTriggers() => new() { Condition.Build(new Condition.IsVictoryPointStep(), this) };
}
