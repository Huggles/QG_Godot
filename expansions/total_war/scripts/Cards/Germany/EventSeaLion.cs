using System.Collections.Generic;

public partial class EventSeaLion : EventCardLogic
{
    private static readonly List<Country> northSea = [Country.NorthSea];
    private static readonly List<Country> unitedKingdom = [Country.UnitedKingdom];

    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        TotalWarCards.BattleTargetsIn(situation.Board, Faction, unitedKingdom);

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.BUILD, UnitType.NAVY, northSea))
            .Plus(TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live)));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.BUILD, UnitType.NAVY, northSea, "Build a Navy in the North Sea"),
        TotalWarCards.BattleStep(this, BattleTargets, "Battle the United Kingdom")
    };
}
