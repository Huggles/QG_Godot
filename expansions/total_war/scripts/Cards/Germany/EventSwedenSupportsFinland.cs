using System.Collections.Generic;

public partial class EventSwedenSupportsFinland : EventCardLogic
{
    private static readonly List<Country> baltic = [Country.BalticSea];
    private static readonly List<Country> scandinavia = [Country.Scandinavia];

    /// <summary>"To use": Germany or the Soviet Union has an Army in Russia. Both steps check it, so neither runs alone.</summary>
    private static bool CanUse(BoardState board) => TotalWarCards.HasArmyIn(board, [Faction.GERMANY, Faction.SOVIET], Country.Russia);

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.BUILD, UnitType.NAVY, baltic))
            .Plus(TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, scandinavia)));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.BUILD, UnitType.NAVY, baltic, "Build a Navy in the Baltic Sea", CanUse),
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, scandinavia, "Recruit an Army in Scandinavia", CanUse)
    };
}
