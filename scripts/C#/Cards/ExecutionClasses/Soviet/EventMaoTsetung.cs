using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventMaoTsetung : EventCardLogic
{
    private List<int> AxisArmiesInChinaOrSzechuan =>
        new List<Country> { Country.China, Country.Szechuan }
            .SelectMany(c => CountryState.ForEnum(c).Units.Values)
            .Where(uId => StaticGameData.FactionTeamForFaction(UnitState.ForId(uId).Faction) == FactionTeam.AXIS
                       && UnitState.ForId(uId).IsArmy
                       && !UnitState.ForId(uId).ImmuneForTurn)
            .ToList();

    /// <summary>The armies this can eliminate.</summary>
    public override TargetSet Targets() => TargetSet.Units(AxisArmiesInChinaOrSzechuan);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new ResultStep(this, async () => {
                int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, AxisArmiesInChinaOrSzechuan).BroadCast()).ResponseUnitIds[0];
                RemoveUnitChangeEvent removeEvent = 
                    new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE);
                return removeEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => AxisArmiesInChinaOrSzechuan.Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in China or Szechuan")
        };
    }
}