using System.Collections.Generic;

public partial class EventShootFirstAndArgueAfterwards : EventCardLogic
{
    /// <summary>What can be battled at sea next to the Navy this card just built.</summary>
    private List<BattleTarget> SeaTargets(GameSituation situation) =>
        TotalWarCards.DeployedBy(situation, this) is { } navyIn
            ? situation.Board.AdjacentBattleTargets(Faction, CountryType.SEA, navyIn)
            : new();

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.BUILD, UnitType.NAVY, TotalWarCards.PacificSeas));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.BUILD, UnitType.NAVY, TotalWarCards.PacificSeas,
            "Build a Navy in a sea space with 'Pacific' in its name"),
        TotalWarCards.BattleStep(this, SeaTargets, "Battle a sea space adjacent to the Navy just built")
            .RequiringPreviousStep()
    };
}
