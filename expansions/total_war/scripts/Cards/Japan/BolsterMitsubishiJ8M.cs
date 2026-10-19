using System.Collections.Generic;
using System.Linq;

/// <summary>"Use at the beginning of your Draw step. Eliminate an Allied Air Force adjacent to your Air Force." French and Chinese Air Forces are Allied too.</summary>
public partial class BolsterMitsubishiJ8M : BolsterCardLogic
{
    private List<int> AlliedAirForces(BoardState board) => board.ActiveAirForceIds(Faction)
        .SelectMany(airForce => board.AdjacentCountryStates(Faction, board.CountryStateOf(UnitState.ForId(airForce))))
        .SelectMany(country => board.EnemyAirForcesIn(Faction, country))
        .Where(unitId => UnitState.ForId(unitId).FactionTeam == FactionTeam.ALLIES)
        .Distinct().ToList();

    public override TargetSet Targets() => TargetSet.Units(AlliedAirForces(BoardState.Live));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.DRAW, s => AlliedAirForces(s.Board).Count > 0);

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.UnitFrom(c => AlliedAirForces(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => AlliedAirForces(s.Board).Count > 0), this))
            .WithGuidance("Eliminate an Allied Air Force next to your Air Force")
    };
}
