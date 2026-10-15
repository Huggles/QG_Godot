using System.Collections.Generic;
using System.Linq;

public partial class EventCashAndCarryReplacesNeutralityActs : EventCardLogic
{
    /// <summary>Where an Allied country could recruit a piece it still has available: in or next to its Home space.</summary>
    private static List<CountryState> RecruitTargets(BoardState board, Faction ally)
    {
        CountryState home = StaticGameData.FactionDataMap[ally].HomeSpaceCountryState;
        List<CountryState> open = new();
        if (board.AvailableUnitCount(ally, UnitType.ARMY) > 0) open.AddRange(board.RecruitableLand(ally));
        if (board.AvailableUnitCount(ally, UnitType.NAVY) > 0) open.AddRange(board.RecruitableSea(ally));
        return open.Where(country => country == home || home.ConnectedCountryStates.Contains(country)).ToList();
    }

    /// <summary>The Allied countries with somewhere to recruit. France and China are left out: the card doesn't name them.</summary>
    private static List<Faction> Allies(BoardState board) =>
        StaticGameData.FactionsForTeam(FactionTeam.ALLIES).Where(ally => RecruitTargets(board, ally).Count > 0).ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(Allies(BoardState.Live).SelectMany(ally => RecruitTargets(BoardState.Live, ally)).Distinct());

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, async () => {
            var allyResp = await new InputRequest.SelectFactionRequestHandler(Faction, Allies(BoardState.Live)).BroadCast();
            Faction ally = (Faction)allyResp.ResponseCardIds[0];
            var countryResp = await new InputRequest.SelectCountryRequestHandler(Faction, RecruitTargets(BoardState.Live, ally).ToCountryIds()).BroadCast();
            return new DeployUnitChangeEvent(ally, countryResp.ResponseCountryIds[0], DeployType.RECRUIT);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => Allies(s.Board).Count > 0), this))
        .WithGuidance("Recruit an available Allied Army or Navy in or adjacent to its Home space")
    };
}
