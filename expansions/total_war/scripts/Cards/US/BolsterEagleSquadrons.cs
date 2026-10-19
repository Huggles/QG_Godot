using System.Collections.Generic;

/// <summary>"Use at the beginning of your Air step. The United Kingdom may deploy an Air Force." The US player places it, as other cards that name an ally.</summary>
public partial class BolsterEagleSquadrons : BolsterCardLogic
{
    public override TargetSet Targets() => TargetSet.Countries(TotalWarCards.AirDeployTargets(BoardState.Live, Faction.UNITED_KINGDOM));

    protected override List<Condition> CardTriggers() =>
        TotalWarCards.AtStepStart(this, TurnStep.AIR, s => TotalWarCards.AirDeployTargets(s.Board, Faction.UNITED_KINGDOM).Count > 0);

    public override List<CardStep> OnActivate() => new() { TotalWarCards.AirDeployStep(this, Faction.UNITED_KINGDOM, "Deploy a British Air Force") };
}
