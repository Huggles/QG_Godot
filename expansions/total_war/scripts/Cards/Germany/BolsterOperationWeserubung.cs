using System.Collections.Generic;

/// <summary>"Use at the beginning of your Victory step. Discard the top 1 card of your draw deck to recruit an Army in Scandinavia."</summary>
public partial class BolsterOperationWeserubung : BolsterCardLogic
{
    private static readonly List<Country> Scandinavia = [Country.Scandinavia];

    private bool CanRecruit(BoardState board) => TotalWarCards.DeployTargets(board, Faction, DeployType.RECRUIT, UnitType.ARMY, Scandinavia).Count > 0;

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, Scandinavia));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.VICTORY_POINT, s => CanRecruit(s.Board));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, Scandinavia,
                "Discard the top card of your draw deck to recruit an Army in Scandinavia", CanRecruit)
            .WithPrice(TotalWarCards.DeckDiscardPrice(this, 1))
    };
}
