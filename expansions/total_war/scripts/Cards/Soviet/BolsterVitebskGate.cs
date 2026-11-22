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
        new ResultStep(this, Choose.UnitFrom(c => AxisArmies(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .WithPrice(TotalWarCards.TypedDiscardPrice(this, CardType.BUILD_ARMY, "Discard a Build Army card"))
            .WithPrice(StepPrice.HandDiscard(this, 2))
            // CanPay counts the whole price at once: the Build Army card and the 2 others.
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => CanPay(s.Board) && AxisArmies(s.Board).Count > 0), this))
            .WithGuidance("Discard a Build Army card and 2 other cards to eliminate an Axis Army in Moscow or Russia")
    };
}
