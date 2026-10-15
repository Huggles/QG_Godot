using System.Collections.Generic;

public partial class EWElektrobootSubmarines : EWCardLogic
{
    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.FactionFrom(_ => StaticGameData.FactionsForTeam(FactionTeam.ALLIES),
                (target, _) => new ForceDiscardCardsChangeEvent(Faction, target, 1)))
            .WithGuidance("Make an Allied country discard the top card of its draw deck"),

        new ResultStep(this, Choose.Fixed(_ => new ScorePointsChangeEvent(new VPEntry(2, "Elektroboot Submarines"), Faction)))
            .WithGuidance("Gain 2 Victory Points")
    };
}
