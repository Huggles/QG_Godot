using System.Collections.Generic;
using System.Linq;

/// <summary>"Use during your Victory step. Score +1 Victory Point for each Japanese Navy that occupies a space with "Pacific" in the title." Offered as the step begins.</summary>
public partial class BolsterThePacificEmpire : BolsterCardLogic
{
    private List<UnitState> PacificNavies(BoardState board) => board.ActiveUnits(Faction)
        .Where(unit => unit.IsNavy && TotalWarCards.IsPacificSea(board.CountryStateOf(unit))).ToList();

    public override TargetSet Targets() => TargetSet.Units(PacificNavies(BoardState.Live));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.VICTORY_POINT, s => PacificNavies(s.Board).Count > 0);

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c => new ScorePointsChangeEvent(
                new VPEntry(PacificNavies(c.Board).Count, "Japanese Navies in the Pacific (The Pacific Empire)"), Faction)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => PacificNavies(s.Board).Count > 0), this))
            .WithGuidance("Gain 1 Victory Point for each Japanese Navy in a Pacific space")
    };
}
