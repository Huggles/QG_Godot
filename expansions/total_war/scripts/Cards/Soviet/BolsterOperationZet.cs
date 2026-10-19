using System.Collections.Generic;

/// <summary>
/// "Use at the beginning of your Air step. Deploy a Chinese Air Force, or a Chinese Air Force may gain air superiority."
/// Air superiority is offered only against an enemy Air Force: an empty space would spend the card for nothing.
/// </summary>
public partial class BolsterOperationZet : BolsterCardLogic
{
    public override TargetSet Targets() => TargetSet.Countries(TotalWarCards.AirDeployTargets(BoardState.Live, Faction.CHINA))
        .Plus(TargetSet.Countries(TotalWarCards.ChineseAirSuperiorityTargets(BoardState.Live)));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.AIR, s => TotalWarCards.CanUseChineseAir(s.Board));

    public override List<CardStep> OnActivate() => new() { TotalWarCards.ChineseAirStep(this) };
}
