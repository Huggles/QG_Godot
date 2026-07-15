using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class EventTransSiberianRailroad : EventCardLogic
{
    private List<int> GetSovietArmyIds() =>
        FactionState.ForEnum(Faction).ActiveUnitIds
            .Where(uId => UnitState.ForId(uId).IsArmy)
            .ToList();

    public override List<CardStep> OnActivate()
    {
        var allArmyIds = GetSovietArmyIds();
        var relocatedIds = new List<int>();

        return Enumerable.Range(0, allArmyIds.Count).Select(_ =>
            new CardStep(this, async () => {
                var eligibleIds = allArmyIds
                    .Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0)
                    .ToList();

                int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, eligibleIds).BroadCast()).ResponseUnitIds[0];
                relocatedIds.Add(selectedUnitId);

                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = false;
                await removeEvent.ApplyChange();

                var buildableIds = CountryState.BuildableLand(Faction).Select(cs => cs.Id).ToList();
                int countryId = (await new InputRequest.SelectCountryRequestHandler(Faction, buildableIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => {
                var eligible = allArmyIds
                    .Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0)
                    .ToList();
                return eligible.Count > 0 && CountryState.BuildableLand(Faction).Count > 0;
            }), this))
            .WithGuidance("Select a Soviet Army to eliminate and rebuild")
        ).ToList();
    }
}