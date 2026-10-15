using System.Collections.Generic;
using System.Linq;

public partial class StatusAbsoluteNationalDefenseSphere : StatusCardLogic, IVPModifier
{
    private List<UnitState> Navies(BoardState board) => board.ActiveUnits(Faction).Where(unit => unit.IsNavy).ToList();

    public override TargetSet Targets() => TargetSet.Units(Navies(BoardState.Live));

    public VPEntry AddVictoryPoints(BoardState board) =>
        new VPEntry(Navies(board).Count >= 3 ? 1 : 0, "3 or more Navies on the board");

    protected override List<Condition> CardTriggers() => new() { Condition.Build(new Condition.IsVictoryPointStep(), this) };
}
