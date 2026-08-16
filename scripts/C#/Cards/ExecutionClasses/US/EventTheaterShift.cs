using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class EventTheaterShift : EventCardLogic
{
    private List<int> GetUSUnitIds =>
        FactionState.ForEnum(Faction).ActiveUnitIds
            .Where(uId => UnitState.ForId(uId) != null)
            .ToList();

    private List<int> relocatedIds = new List<int>();
    private bool lastRemovedUnitWasNavy = false;

    public override List<CardStep> OnActivate() => MakeRelocationSteps();

    private List<CardStep> MakeRelocationSteps() =>
        new List<CardStep> { MakeRemovalStep(), MakeDeployStep() };

    private CardStep MakeRemovalStep()
    {
        return new CardStep(this, async () =>
        {
            var eligibleIds = GetUSUnitIds
                .Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0)
                .ToList();
            int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, eligibleIds).BroadCast()).ResponseUnitIds[0];
            lastRemovedUnitWasNavy = UnitState.ForId(selectedUnitId).Type == UnitType.NAVY;
            relocatedIds.Add(selectedUnitId);
            bool hasMoreUnits = GetUSUnitIds.Any(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0);
            if (hasMoreUnits) CardSteps.AddRange(MakeRelocationSteps());
            RemoveUnitChangeEvent removeEvent = BuildChangeEvent(new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
            removeEvent.IsTrigger = false;
            await CardPlayPool.DoChangeEvent(removeEvent);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
        {
            var eligible = GetUSUnitIds.Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0).ToList();
            return eligible.Count > 0 && (CountryState.BuildableLand(Faction).Count > 0 || CountryState.BuildableSea(Faction).Count > 0);
        }), this))
        .WithGuidance("Select a US Army or Navy to eliminate and rebuild");
    }

    private CardStep MakeDeployStep()
    {
        return new CardStep(this, async () =>
        {
            await Task.Delay(1000);
            var buildableIds = lastRemovedUnitWasNavy
                ? CountryState.BuildableSea(Faction).Select(cs => cs.Id).ToList()
                : CountryState.BuildableLand(Faction).Select(cs => cs.Id).ToList();
            int countryId = (await new InputRequest.SelectCountryRequestHandler(Faction, buildableIds).BroadCast()).ResponseCountryIds[0];
            DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
            deployEvent.IsTrigger = true;
            await CardPlayPool.DoChangeEvent(deployEvent);
        })
        .WithGuidance("Select where to rebuild the US piece");
    }
}