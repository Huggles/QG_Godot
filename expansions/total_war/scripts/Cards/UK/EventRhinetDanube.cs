using System.Collections.Generic;

public partial class EventRhinetDanube : EventCardLogic
{
    /// <summary>France does the battling, so what it may attack is exactly what lies next to its supplied pieces.</summary>
    private static List<BattleTarget> BattleTargets(GameSituation situation) =>
        TotalWarCards.BattleTargetsOn(situation.Board, Faction.FRANCE, CountryType.LAND);

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.BattleStepFor(this, Faction.FRANCE, BattleTargets, "Battle a land space adjacent to a supplied French piece")
    };
}
