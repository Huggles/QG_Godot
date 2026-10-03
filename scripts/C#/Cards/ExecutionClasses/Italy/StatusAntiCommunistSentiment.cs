using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusAntiCommunistSentiment : StatusCardLogic, IVPModifier
{
    private static readonly List<Country> scoringCountries = [Country.Ukraine, Country.Russia];

    /// <summary>The pieces that score. Read by both the VP count and <see cref="Targets"/>.</summary>
    private List<UnitState> ScoringUnits(BoardState board) =>
        board.ActiveUnits(Faction)
            .Where(unitState => scoringCountries.Contains(board.CountryStateOf(unitState).Country))
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits(BoardState.Live));

    public virtual VPEntry AddVictoryPoints(BoardState board)
    {
        return VPEntry.ForUnits(ScoringUnits(board), board, 1, $"{FactionState.ForEnum(Faction).FactionData.FactionAdjactiveLabel} armies in {CountryState.ForEnum(scoringCountries[0]).Label} and {CountryState.ForEnum(scoringCountries[1]).Label}");
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.IsVictoryPointStep(), this) };
    }
}