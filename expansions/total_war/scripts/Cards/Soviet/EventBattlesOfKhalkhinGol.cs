using System.Collections.Generic;

public partial class EventBattlesOfKhalkhinGol : EventCardLogic
{
    private static readonly List<Country> mongolia = [Country.Mongolia];
    private static readonly List<Country> battlegrounds = [Country.Vladivostok, Country.China];

    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        TotalWarCards.BattleTargetsIn(situation.Board, Faction, battlegrounds);

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, mongolia))
            .Plus(TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live)));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, mongolia, "Recruit an Army in Mongolia"),
        TotalWarCards.BattleStep(this, BattleTargets, "Battle Vladivostok or China")
    };
}
