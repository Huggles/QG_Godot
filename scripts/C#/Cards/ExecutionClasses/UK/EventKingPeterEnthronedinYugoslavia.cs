using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventKingPeterEnthronedinYugoslavia : EventCardLogic
{
    private List<int> AxisArmiesInBalkans(BoardState board) =>
        board.UnitsIn(CountryState.ForEnum(Country.Balkans)).Values
            .Where(uId => StaticGameData.FactionTeamForFaction(UnitState.ForId(uId).Faction) == FactionTeam.AXIS
                       && UnitState.ForId(uId).IsArmy
                       && !board.ImmuneForTurn(UnitState.ForId(uId)))
            .ToList();

    /// <summary>The Axis armies this can clear out, and the space the replacement lands in.</summary>
    public override TargetSet Targets() =>
        TargetSet.Units(AxisArmiesInBalkans(BoardState.Live))
            .Plus(TargetSet.Countries(new List<Country> { Country.Balkans }));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Eliminate an Axis Army in the Balkans
            new ResultStep(this, Choose.UnitFrom(c => AxisArmiesInBalkans(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => AxisArmiesInBalkans(s.Board).Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in the Balkans"),

            // Recruit an Army in the Balkans
            new ResultStep(this, Choose.Fixed(_ => new DeployUnitChangeEvent(Faction, (int)Country.Balkans, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Balkans], Faction), this))
            .WithGuidance("Recruit an Army in the Balkans")
        };
    }
}