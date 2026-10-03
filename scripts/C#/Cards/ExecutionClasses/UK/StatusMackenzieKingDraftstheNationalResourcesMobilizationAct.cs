using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusMackenzieKingDraftstheNationalResourcesMobilizationAct : StatusCardLogic, IVPModifier
{
    /// <summary>The two pieces that score, either of which may be absent. Read by both the VP count
    /// and <see cref="Targets"/>, so the preview shows exactly what is earning the points.</summary>
    private List<UnitState> ScoringUnits(BoardState board) =>
        board.ActiveUnits(Faction)
            .Where(u => (board.CountryStateOf(u).Country == Country.NorthAtlantic && u.Type == UnitType.NAVY)
                     || (board.CountryStateOf(u).Country == Country.Canada && u.Type == UnitType.ARMY))
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits(BoardState.Live));

    public virtual VPEntry AddVictoryPoints(BoardState board)
    {
        // One point per QUALIFYING SPACE, not per piece: two navies in the North Atlantic score one.
        return VPEntry.ForCountries(ScoringUnits(board).Select(board.CountryOf), 1, $"a navy in {CountryState.ForEnum(Country.NorthAtlantic).Label} and army in {CountryState.ForEnum(Country.Canada).Label}");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}