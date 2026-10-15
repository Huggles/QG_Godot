using System.Collections.Generic;

public partial class EWArkhangelskFalls : EWCardLogic
{
    /// <summary>"To use": Axis Armies in both Scandinavia and Russia.</summary>
    private static bool CanUse(BoardState board)
    {
        List<Faction> axis = StaticGameData.FactionsForTeam(FactionTeam.AXIS);
        return TotalWarCards.HasArmyIn(board, axis, Country.Scandinavia) && TotalWarCards.HasArmyIn(board, axis, Country.Russia);
    }

    private Condition Usable() => Condition.Build(new Condition.CustomCondition(s => CanUse(s.Board)), this);

    public override TargetSet Targets() => TargetSet.Countries(new List<Country> { Country.Scandinavia, Country.Russia });

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction.SOVIET, 5)))
            .WithCondition(Usable)
            .WithGuidance("Make the Soviet Union discard the top 5 cards of its draw deck"),

        new ResultStep(this, Choose.Fixed(_ => new ScorePointsChangeEvent(new VPEntry(1, "Arkhangelsk Falls"), Faction)))
            .WithCondition(Usable)
            .WithGuidance("Gain 1 Victory Point")
    };
}
