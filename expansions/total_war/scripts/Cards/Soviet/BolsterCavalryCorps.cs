using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "Use at the beginning of your Play step. Discard a Build Army card from your hand and eliminate a Soviet
/// Army from the board. Then build an Army." The elimination is part of the price, so it opens no window.
/// </summary>
public partial class BolsterCavalryCorps : BolsterCardLogic
{
    private List<int> SovietArmies(BoardState board) => board.ActiveUnits(Faction).Where(unit => unit.IsArmy).Select(unit => unit.Id).ToList();

    public override TargetSet Targets() => TargetSet.Units(SovietArmies(BoardState.Live));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtPlayStepStart(this)
        .Append(Condition.Build(new Condition.CustomCondition(s => SovietArmies(s.Board).Count > 0), this))
        .ToList();

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.TypedDiscardStep(this, CardType.BUILD_ARMY, "Discard a Build Army card and eliminate a Soviet Army, then build an Army",
            s => SovietArmies(s.Board).Count > 0),
        new RequirementStep(this, Choose.UnitFrom(c => SovietArmies(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board), allowSkip: false))
            .RequiringPreviousStep()
            .WithGuidance("Eliminate one of your Armies"),
        TotalWarCards.DeployStep(this, DeployType.BUILD, UnitType.ARMY, null, "Build an Army").RequiringPreviousStep()
    };
}
