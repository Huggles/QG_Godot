using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventMaoTsetung : EventCardLogic
{
    private List<int> AxisArmiesInChinaOrSzechuan(BoardState board) =>
        new List<Country> { Country.China, Country.Szechuan }
            .SelectMany(c => board.UnitsIn(CountryState.ForEnum(c)).Values)
            .Where(uId => StaticGameData.FactionTeamForFaction(UnitState.ForId(uId).Faction) == FactionTeam.AXIS
                       && UnitState.ForId(uId).IsArmy
                       && !board.ImmuneForTurn(UnitState.ForId(uId)))
            .ToList();

    /// <summary>The armies this can eliminate.</summary>
    public override TargetSet Targets() => TargetSet.Units(AxisArmiesInChinaOrSzechuan(BoardState.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new ResultStep(this, Choose.UnitFrom(c => AxisArmiesInChinaOrSzechuan(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => AxisArmiesInChinaOrSzechuan(s.Board).Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in China or Szechuan")
        };
    }
}