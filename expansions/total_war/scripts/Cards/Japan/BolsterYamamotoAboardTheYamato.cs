using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "Use at the beginning of your Play step. Discard a Response card from your hand to deploy an Air Force in a
/// space with your supplied Navy." Needs an Air Force left in the pool and a Navy space without one.
/// </summary>
public partial class BolsterYamamotoAboardTheYamato : BolsterCardLogic
{
    private List<int> DeployTargets(BoardState board) => board.AvailableUnitCount(Faction, UnitType.AIR_FORCE) == 0
        ? new()
        : board.ActiveUnits(Faction)
            .Where(unit => unit.IsNavy && board.InSupply(unit) && board.CanDeployAirForce(Faction, board.CountryStateOf(unit)))
            .Select(unit => board.CountryOf(unit)).Distinct().ToList();

    public override TargetSet Targets() => TargetSet.Countries(DeployTargets(BoardState.Live));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtPlayStepStart(this)
        .Append(Condition.Build(new Condition.CustomCondition(s => DeployTargets(s.Board).Count > 0), this))
        .ToList();

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.TypedDiscardStep(this, CardType.RESPONSE, "Discard a Response card to deploy an Air Force with your supplied Navy",
            s => DeployTargets(s.Board).Count > 0),
        new ResultStep(this, Choose.CountryFrom(c => DeployTargets(c.Board), (countryId, _) => new DeployAirForceChangeEvent(Faction, countryId)))
            .RequiringPreviousStep()
            .WithGuidance("Deploy an Air Force with your supplied Navy")
    };
}
