using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class EventGeneralWinter : EventCardLogic
{
    private List<int> AxisArmiesNearMoscow(BoardState board)
    {
        var moscowCs = CountryState.ForEnum(Country.Moscow);
        return new List<CountryState> { moscowCs }
            .Concat(moscowCs.ConnectedCountryStates)
            .SelectMany(cs => board.UnitsIn(cs).Values)
            .Where(uId => {
                var u = UnitState.ForId(uId);
                return StaticGameData.FactionTeamForFaction(u.Faction) == FactionTeam.AXIS && u.IsArmy && !board.ImmuneForTurn(u);
            })
            .ToList();
    }

    /// <summary>Both steps eliminate from this same list.</summary>
    public override TargetSet Targets() => TargetSet.Units(AxisArmiesNearMoscow(BoardState.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new ResultStep(this, Choose.UnitFrom(c => AxisArmiesNearMoscow(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => AxisArmiesNearMoscow(s.Board).Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in or adjacent to Moscow"),

            new ResultStep(this, Choose.UnitFrom(c => AxisArmiesNearMoscow(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => AxisArmiesNearMoscow(s.Board).Count > 0), this))
            .WithGuidance("Eliminate a second Axis Army in or adjacent to Moscow")
        };
    }
}