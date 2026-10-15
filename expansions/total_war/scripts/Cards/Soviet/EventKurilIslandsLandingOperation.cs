using System.Collections.Generic;

public partial class EventKurilIslandsLandingOperation : EventCardLogic
{
    private static readonly List<Country> seaOfJapan = [Country.SeaOfJapan];
    private static readonly List<Country> japan = [Country.Japan];

    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        TotalWarCards.BattleTargetsIn(situation.Board, Faction, japan);

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.BUILD, UnitType.NAVY, seaOfJapan))
            .Plus(TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live)));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.BUILD, UnitType.NAVY, seaOfJapan, "Build a Navy in the Sea of Japan"),
        TotalWarCards.BattleStep(this, BattleTargets, "Battle Japan")
    };
}
