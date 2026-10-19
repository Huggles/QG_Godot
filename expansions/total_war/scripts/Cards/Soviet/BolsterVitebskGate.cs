using System.Collections.Generic;
using System.Linq;

/// <summary>"Use at the beginning of your Victory step. Discard a Build Army card and 2 other cards from your hand to eliminate an Axis Army in Moscow or Russia."</summary>
public partial class BolsterVitebskGate : BolsterCardLogic
{
    private static readonly List<Country> Front = [Country.Moscow, Country.Russia];

    private List<int> AxisArmies(BoardState board) => TotalWarCards.TeamUnitsIn(board, FactionTeam.AXIS, UnitType.ARMY, Front)
        .Where(unit => !board.ImmuneForTurn(unit)).Select(unit => unit.Id).ToList();

    /// <summary>A Build Army card and 2 more besides this one.</summary>
    private bool CanPay(BoardState board) =>
        TotalWarCards.InHand(board, Faction, CardType.BUILD_ARMY).Count > 0 && TotalWarCards.OtherHandCards(board, this) >= 3;

    public override TargetSet Targets() => TargetSet.Units(AxisArmies(BoardState.Live));

    protected override List<Condition> CardTriggers() =>
        TotalWarCards.AtStepStart(this, TurnStep.VICTORY_POINT, s => CanPay(s.Board) && AxisArmies(s.Board).Count > 0);

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.TypedDiscardStep(this, CardType.BUILD_ARMY, "Discard a Build Army card and 2 other cards to eliminate an Axis Army in Moscow or Russia",
            s => CanPay(s.Board) && AxisArmies(s.Board).Count > 0),
        TotalWarCards.HandDiscardStep(this, 2, "Discard 2 more cards").RequiringPreviousStep(),
        new ResultStep(this, Choose.UnitFrom(c => AxisArmies(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .RequiringPreviousStep()
            .WithGuidance("Eliminate an Axis Army in Moscow or Russia")
    };
}
