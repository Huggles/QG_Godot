using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWIndianOceanPatrols : EWCardLogic
{
    /// <summary>The Japanese Navies in or beside the Bay of Bengal: two VP and two discards each. Read by
    /// both the step and <see cref="Targets"/>, so hovering shows exactly what this card is worth.</summary>
    private List<UnitState> ScoringUnits(BoardState board)
    {
        CountryState bayOfBengal = CountryState.ForEnum(Country.BayOfBengal);
        return board.ActiveUnits(Faction)
            .Where(u => u.Type == UnitType.NAVY
                     && (board.CountryOf(u) == (int)Country.BayOfBengal
                         || bayOfBengal.ConnectedCountryIds.Contains(board.CountryOf(u))))
            .ToList();
    }

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits(BoardState.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.Fixed(c => {
                int count = ScoringUnits(c.Board).Count;
                return count == 0 ? null
                    : new ScorePointsChangeEvent(VPEntry.ForUnits(ScoringUnits(c.Board), c.Board, 2, "Japanese Navies in or adjacent to Bay of Bengal"), Faction);
            })),

            new ResultStep(this, Choose.Fixed(c => ScoredBy(c.Previous) == 0 ? null
                : new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, ScoredBy(c.Previous))))
            .RequiringPreviousStep()
        };
    }
}
