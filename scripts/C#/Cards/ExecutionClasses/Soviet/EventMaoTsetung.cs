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

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, AxisArmiesInChinaOrSzechuan).BroadCast()).ResponseUnitIds[0];
                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(
                    new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(removeEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => AxisArmiesInChinaOrSzechuan.Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in China or Szechuan")
        };
    }
}