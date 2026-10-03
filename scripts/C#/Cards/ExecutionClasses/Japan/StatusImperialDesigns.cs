using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusImperialDesigns : StatusCardLogic, IVPModifier
{
    private static readonly List<Country> scoringCountries = [Country.IwoJima, Country.Philippines];

    /// <summary>The pieces that score. Read by both the VP count and <see cref="Targets"/>, so
    /// hovering shows exactly what is earning this card its points.</summary>
    private List<UnitState> ScoringUnits(BoardState board) =>
        board.ActiveUnits(Faction)
            .Where(unitState => scoringCountries.Contains(board.CountryStateOf(unitState).Country))
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits(BoardState.Live));

    /// <summary>A flat 1 however many spaces are held, shown on the first held one in list order.</summary>
    public virtual VPEntry AddVictoryPoints(BoardState board)
    {
        string reason = $"{FactionState.ForEnum(Faction).FactionData.FactionAdjactiveLabel} army in {CountryState.ForEnum(scoringCountries[0]).Label} or {CountryState.ForEnum(scoringCountries[1]).Label}";
        List<UnitState> units = ScoringUnits(board);
        Country? first = scoringCountries.Cast<Country?>().FirstOrDefault(c => units.Any(u => board.CountryStateOf(u).Country == c));
        return first == null ? new VPEntry(0, reason) : VPEntry.ForCountry(1, reason, (int)first);
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}