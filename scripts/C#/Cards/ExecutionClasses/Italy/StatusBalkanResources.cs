using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusBalkanResources : StatusCardLogic, IVPModifier
{
    /// <summary>The Italian Armies in the Balkans. The card scores at most one point however many
    /// there are, but the preview shows them all — they are what is holding the point.</summary>
    private List<UnitState> ScoringUnits(BoardState board) =>
        board.ActiveUnits(Faction)
            .Where(us => board.CountryStateOf(us).Country == Country.Balkans && us.Type == UnitType.ARMY)
            .ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(new List<Country> { Country.Balkans }).Plus(TargetSet.Units(ScoringUnits(BoardState.Live)));

    public virtual VPEntry AddVictoryPoints(BoardState board)
    {
        const string reason = "an Italian Army in the Balkans";
        UnitState army = ScoringUnits(board).FirstOrDefault();
        return army != null ? VPEntry.ForUnit(1, reason, army) : new VPEntry(0, reason);
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}