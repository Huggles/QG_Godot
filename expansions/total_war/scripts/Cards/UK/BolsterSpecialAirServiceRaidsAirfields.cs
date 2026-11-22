using System.Collections.Generic;
using System.Linq;

/// <summary>"Use at the beginning of your Play step. Discard 2 cards from your hand to eliminate an Axis Air Force adjacent to your Air Force."</summary>
public partial class BolsterSpecialAirServiceRaidsAirfields : BolsterCardLogic
{
    private List<int> AxisAirForces(BoardState board) => board.ActiveAirForceIds(Faction)
        .SelectMany(airForce => board.AdjacentCountryStates(Faction, board.CountryStateOf(UnitState.ForId(airForce))))
        .SelectMany(country => board.EnemyAirForcesIn(Faction, country))
        .Where(unitId => UnitState.ForId(unitId).FactionTeam == FactionTeam.AXIS)
        .Distinct().ToList();

    public override TargetSet Targets() => TargetSet.Units(AxisAirForces(BoardState.Live));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtPlayStepStart(this)
        .Append(Condition.Build(new Condition.CustomCondition(s => AxisAirForces(s.Board).Count > 0), this))
        .ToList();

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.UnitFrom(c => AxisAirForces(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .WithPrice(TotalWarCards.HandDiscardPrice(this, 2))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => AxisAirForces(s.Board).Count > 0), this))
            .WithGuidance("Discard 2 cards to eliminate an Axis Air Force next to your Air Force")
    };
}
