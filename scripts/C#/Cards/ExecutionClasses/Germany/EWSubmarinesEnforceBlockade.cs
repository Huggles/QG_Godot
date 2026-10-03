using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesEnforceBlockade : EWCardLogic
{
    /// <summary>The German Armies beside the North Sea: one VP and two UK discards each. Read by
    /// both the step and <see cref="Targets"/>.</summary>
    private List<UnitState> ScoringUnits(BoardState board)
    {
        CountryState northSea = CountryState.ForEnum(Country.NorthSea);
        return board.ActiveUnits(Faction)
            .Where(u => u.Type == UnitType.ARMY && northSea.ConnectedCountryIds.Contains(board.CountryOf(u)))
            .ToList();
    }

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits(BoardState.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.Fixed(c =>
                    new ScorePointsChangeEvent(VPEntry.ForUnits(ScoringUnits(c.Board), 1, "German Armies adjacent to North Sea"), Faction))),

            new ResultStep(this, Choose.Fixed(c => new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, ScoredBy(c.Previous) * 2)))
            .RequiringPreviousStep()
        };
    }
}
