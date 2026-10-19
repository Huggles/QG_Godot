using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "Use at the beginning of your Play step. If you have an Air Force on the board, all German pieces are in
/// supply for the rest of the turn." Through GrantSupplyChangeEvent, as Truk: the pieces on the board now.
/// </summary>
public partial class BolsterJu52sAirliftSupplies : BolsterCardLogic
{
    public override TargetSet Targets() => TargetSet.Units(TotalWarCards.Pieces(BoardState.Live, Faction));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtPlayStepStart(this)
        .Append(Condition.Build(new Condition.CustomCondition(s => s.Board.ActiveAirForceIds(Faction).Count > 0), this))
        .ToList();

    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(c =>
                new GrantSupplyChangeEvent(Faction, TotalWarCards.Pieces(c.Board, Faction).Select(unit => unit.Id).ToList())))
            .WithGuidance("Put every German piece in supply for the rest of the turn")
    };
}
