using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesSupportPacificIslands : EWCardLogic
{
    /// <summary>The Japanese Navies in or beside the East Pacific: two VP and two discards each. Read by
    /// both the step and <see cref="Targets"/>, so hovering shows exactly what this card is worth.</summary>
    private List<UnitState> ScoringUnits(BoardState board)
    {
        CountryState eastPacific = CountryState.ForEnum(Country.EastPacific);
        return board.ActiveUnits(Faction)
            .Where(u => u.Type == UnitType.NAVY
                     && (board.CountryOf(u) == (int)Country.EastPacific
                         || eastPacific.ConnectedCountryIds.Contains(board.CountryOf(u))))
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
                    : new ScorePointsChangeEvent(new VPEntry(count * 2, "Japanese Navies in or adjacent to East Pacific"), Faction);
            })),

            new ResultStep(this, Choose.Fixed(c => ScoredBy(c.Previous) == 0 ? null
                : new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_STATES, ScoredBy(c.Previous))))
            .RequiringPreviousStep()
        };
    }
}
