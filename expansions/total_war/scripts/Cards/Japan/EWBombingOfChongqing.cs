using System.Collections.Generic;
using System.Linq;

public partial class EWBombingOfChongqing : EWCardLogic
{
    /// <summary>The Japanese Air Forces in or next to Szechuan; each scores 1 VP and costs the US 2 cards.</summary>
    private List<UnitState> Bombers(BoardState board)
    {
        CountryState szechuan = CountryState.ForEnum(Country.Szechuan);
        return UnitState.ForIds(board.ActiveAirForceIds(Faction))
            .Where(unit => board.CountryOf(unit) == szechuan.Id || szechuan.ConnectedCountryIds.Contains(board.CountryOf(unit)))
            .ToList();
    }

    private Condition AnyBomber() => Condition.Build(new Condition.CustomCondition(s => Bombers(s.Board).Count > 0), this);

    public override TargetSet Targets() => TargetSet.Units(Bombers(BoardState.Live));

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c => new ScorePointsChangeEvent(VPEntry.ForUnits(Bombers(c.Board), 1, "Japanese Air Forces in or adjacent to Szechuan"), Faction)))
            .WithCondition(AnyBomber)
            .WithGuidance("Gain 1 Victory Point for each Japanese Air Force in or adjacent to Szechuan"),

        new ResultStep(this, Choose.Fixed(c => new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_STATES, 2 * Bombers(c.Board).Count)))
            .WithCondition(AnyBomber)
            .WithGuidance("Make the United States discard 2 cards for each")
    };
}
