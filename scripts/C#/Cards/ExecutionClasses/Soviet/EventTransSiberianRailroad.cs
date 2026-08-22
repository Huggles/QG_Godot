using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class EventTransSiberianRailroad : EventCardLogic
{
    private List<int> GetSovietArmyIds =>
        FactionState.ForEnum(Faction).ActiveUnitIds
            .Where(uId => UnitState.ForId(uId).IsArmy)
            .ToList();

    private List<int> relocatedIds = new List<int>();

    public override List<CardStep> OnActivate()
    {
        return MakeRelocationSteps();
    }

    private List<CardStep> MakeRelocationSteps()
    {
        return new List<CardStep> { MakeRemovalStep(), MakeDeployStep() };
    }

    private CardStep MakeRemovalStep()
    {
        return new CardStep(this, async () =>
        {
            var eligibleIds = GetSovietArmyIds
                .Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0)
                .ToList();

            int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, eligibleIds).BroadCast()).ResponseUnitIds[0];
            relocatedIds.Add(selectedUnitId);

            bool hasMoreUnits = GetSovietArmyIds.Any(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0);
            if (hasMoreUnits) CardSteps.AddRange(MakeRelocationSteps());

            RemoveUnitChangeEvent removeEvent = BuildChangeEvent(new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
            removeEvent.IsTrigger = false;
            await CardPlayPool.DoChangeEvent(removeEvent);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
        {
            var eligible = GetSovietArmyIds
                .Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0)
                .ToList();
            return eligible.Count > 0 && CountryState.BuildableLand(Faction).Count > 0;
        }), this))
        .WithGuidance("Select a Soviet Army to eliminate and rebuild");
    }

    private CardStep MakeDeployStep() {
        return new CardStep(this, async () =>
        {
            await Task.Delay(1000);
            var buildableIds = CountryState.BuildableLand(Faction).Select(cs => cs.Id).ToList();
            int countryId = (await new InputRequest.SelectCountryRequestHandler(Faction, buildableIds).BroadCast()).ResponseCountryIds[0];
            DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
            deployEvent.IsTrigger = true;
            await CardPlayPool.DoChangeEvent(deployEvent);
        })
        // Rebuilding is the other half of the removal, not an effect of its own: a skipped removal
        // finishes the card instead of granting a free army.
        .RequiringPreviousStep()
        .WithGuidance("Select where to rebuild the Soviet Army");
    }
}