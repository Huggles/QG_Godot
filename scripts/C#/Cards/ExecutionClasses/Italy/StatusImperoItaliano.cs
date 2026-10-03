using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusImperoItaliano : StatusCardLogic, IVPModifier
{
    private static readonly List<Country> scoringCountries = [Country.NorthAfrica, Country.Africa, Country.MiddleEast];
    private static readonly List<Faction> scoringFactions = [Faction.GERMANY, Faction.JAPAN, Faction.ITALY];

    /// <summary>Every Axis Army in the three spaces — the whole team's, not just Italy's, which is
    /// what makes this card worth previewing before you commit to it.</summary>
    private List<UnitState> ScoringUnits(BoardState board) =>
        scoringFactions
            .SelectMany(board.ActiveUnits)
            .Where(unitState => scoringCountries.Contains(board.CountryStateOf(unitState).Country)
                             && unitState.Type == UnitType.ARMY)
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits(BoardState.Live));

    public virtual VPEntry AddVictoryPoints(BoardState board)
    {
        return VPEntry.ForUnits(ScoringUnits(board), 1, $"axis armies in {CountryState.ForEnum(scoringCountries[0]).Label}, {CountryState.ForEnum(scoringCountries[1]).Label} and {CountryState.ForEnum(scoringCountries[2]).Label}");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}