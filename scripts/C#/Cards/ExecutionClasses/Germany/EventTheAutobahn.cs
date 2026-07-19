using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class EventTheAutobahn : EventCardLogic
{
    private List<int> GetGermanArmyIds =>
        FactionState.ForEnum(Faction).ActiveUnitIds
            .Where(uId => UnitState.ForId(uId)?.IsArmy == true)
            .ToList();

    private List<int> relocatedIds = new List<int>();

    public override List<CardStep> OnActivate() => MakeRelocationSteps();

    private List<CardStep> MakeRelocationSteps() =>
        new List<CardStep> { MakeRemovalStep(), MakeDeployStep() };

    private CardStep MakeRemovalStep()
    {
        return new CardStep(this, async () =>
        {
            var eligibleIds = GetGermanArmyIds
                .Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0)
                .ToList();
            int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, eligibleIds).BroadCast()).ResponseUnitIds[0];
            relocatedIds.Add(selectedUnitId);
            bool hasMoreUnits = GetGermanArmyIds.Any(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0);
            if (hasMoreUnits) CardSteps.AddRange(MakeRelocationSteps());
            RemoveUnitChangeEvent removeEvent = BuildChangeEvent(new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
            removeEvent.IsTrigger = false;
            return removeEvent;
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
        {
            var eligible = GetGermanArmyIds.Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0).ToList();
            return eligible.Count > 0 && CountryState.BuildableLand(Faction).Count > 0;
        }), this))
        .WithGuidance("Select a German Army to eliminate and rebuild");
    }

    private CardStep MakeDeployStep()
    {
        return new CardStep(this, async () =>
        {
            await Task.Delay(1000);
            var buildableIds = CountryState.BuildableLand(Faction).Select(cs => cs.Id).ToList();
            int countryId = (await new InputRequest.SelectCountryRequestHandler(Faction, buildableIds).BroadCast()).ResponseCountryIds[0];
            DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
            deployEvent.IsTrigger = true;
            return deployEvent;
        })
        .WithGuidance("Select where to rebuild the German Army");
    }
}