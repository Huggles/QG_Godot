using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventTitosPartisans : EventCardLogic
{
    private List<int> AxisArmiesInBalkans(BoardState board) =>
        board.UnitsIn(CountryState.ForEnum(Country.Balkans)).Values
            .Where(uId => StaticGameData.FactionTeamForFaction(UnitState.ForId(uId).Faction) == FactionTeam.AXIS
                       && UnitState.ForId(uId).IsArmy
                       && !board.ImmuneForTurn(UnitState.ForId(uId)))
            .ToList();

    /// <summary>The Axis armies step 1 clears out, and the space step 2 recruits into.</summary>
    public override TargetSet Targets() =>
        TargetSet.Units(AxisArmiesInBalkans(BoardState.Live))
            .Plus(TargetSet.Countries(new List<Country> { Country.Balkans }));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Step 1: Eliminate an Axis Army in the Balkans
            new ResultStep(this, Choose.UnitFrom(c => AxisArmiesInBalkans(c.Board),
                (unitId, _) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => AxisArmiesInBalkans(s.Board).Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in the Balkans"),

            // Step 2: Recruit a Soviet or United Kingdom Army in the Balkans
            new ResultStep(this, Choose.FactionFrom(_ => new List<Faction> { Faction.SOVIET, Faction.UNITED_KINGDOM },
                (faction, _) => new DeployUnitChangeEvent(faction, (int)Country.Balkans, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
                Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Balkans], Faction.SOVIET), this).MeetCondition(s) ||
                Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Balkans], Faction.UNITED_KINGDOM), this).MeetCondition(s)
            ), this))
            .WithGuidance("Recruit a Soviet or United Kingdom Army in the Balkans")
        };
    }
}