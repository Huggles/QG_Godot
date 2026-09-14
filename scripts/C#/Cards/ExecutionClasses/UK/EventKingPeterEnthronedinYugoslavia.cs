using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventKingPeterEnthronedinYugoslavia : EventCardLogic
{
    private List<int> AxisArmiesInBalkans =>
        CountryState.ForEnum(Country.Balkans).Units.Values
            .Where(uId => StaticGameData.FactionTeamForFaction(UnitState.ForId(uId).Faction) == FactionTeam.AXIS
                       && UnitState.ForId(uId).IsArmy
                       && !UnitState.ForId(uId).ImmuneForTurn)
            .ToList();

    /// <summary>The Axis armies this can clear out, and the space the replacement lands in.</summary>
    public override TargetSet Targets() =>
        TargetSet.Units(AxisArmiesInBalkans)
            .Plus(TargetSet.Countries(new List<Country> { Country.Balkans }));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Eliminate an Axis Army in the Balkans
            new ResultStep(this, async () => {
                int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, AxisArmiesInBalkans).BroadCast()).ResponseUnitIds[0];
                RemoveUnitChangeEvent removeEvent = 
                    new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE);
                return removeEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => AxisArmiesInBalkans.Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in the Balkans"),

            // Recruit an Army in the Balkans
            new ResultStep(this, async () => {
                DeployUnitChangeEvent deployEvent = 
                    new DeployUnitChangeEvent(Faction, (int)Country.Balkans, DeployType.RECRUIT);
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Balkans], Faction), this))
            .WithGuidance("Recruit an Army in the Balkans")
        };
    }
}