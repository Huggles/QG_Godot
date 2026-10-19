using System.Collections.Generic;

/// <summary>"Use at the beginning of your Air step. Deploy a French Air Force."</summary>
public partial class BolsterArmeedelAir : BolsterCardLogic
{
    public override TargetSet Targets() => TargetSet.Countries(TotalWarCards.AirDeployTargets(BoardState.Live, Faction.FRANCE));

    protected override List<Condition> CardTriggers() =>
        TotalWarCards.AtStepStart(this, TurnStep.AIR, s => TotalWarCards.AirDeployTargets(s.Board, Faction.FRANCE).Count > 0);

    public override List<CardStep> OnActivate() => new() { TotalWarCards.AirDeployStep(this, Faction.FRANCE, "Deploy a French Air Force") };
}
