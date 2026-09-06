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

    /// <summary>
    /// The pieces still awaiting relocation. Read by the removal step, its condition and
    /// <see cref="Targets"/> alike, so the three cannot disagree about who is left.
    /// </summary>
    private List<int> EligibleUnitIds =>
        GetUSUnitIds
            .Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0)
            .ToList();

    /// <summary>Every piece this will pick up, and every space it could set one down in. Both types,
    /// since which one the rebuild offers depends on the piece the player picks first.</summary>
    public override TargetSet Targets() =>
        TargetSet.Units(EligibleUnitIds)
            .Plus(TargetSet.Countries(CountryState.BuildableLand(Faction)))
            .Plus(TargetSet.Countries(CountryState.BuildableSea(Faction)));

    public override List<CardStep> OnActivate() => MakeRelocationSteps();

    private List<CardStep> MakeRelocationSteps() =>
        new List<CardStep> { MakeRemovalStep(), MakeDeployStep() };

    private CardStep MakeRemovalStep()
    {
        return new CardStep(this, async () =>
        {
            int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, EligibleUnitIds).BroadCast()).ResponseUnitIds[0];
            lastRemovedUnitWasNavy = UnitState.ForId(selectedUnitId).Type == UnitType.NAVY;
            relocatedIds.Add(selectedUnitId);
            if (EligibleUnitIds.Count > 0) CardSteps.AddRange(MakeRelocationSteps());
            RemoveUnitChangeEvent removeEvent = BuildChangeEvent(new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
            removeEvent.IsTrigger = false;
            await CardPlayPool.DoChangeEvent(removeEvent);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
            EligibleUnitIds.Count > 0
            && (CountryState.BuildableLand(Faction).Count > 0 || CountryState.BuildableSea(Faction).Count > 0)), this))
        .WithGuidance("Select a US Army or Navy to eliminate and rebuild");
    }

    private CardStep MakeDeployStep()
    {
        return new CardStep(this, async () =>
        {
            await ReplayContext.Pace(1000);
            var buildableIds = lastRemovedUnitWasNavy
                ? CountryState.BuildableSea(Faction).Select(cs => cs.Id).ToList()
                : CountryState.BuildableLand(Faction).Select(cs => cs.Id).ToList();
            int countryId = (await new InputRequest.SelectCountryRequestHandler(Faction, buildableIds).BroadCast()).ResponseCountryIds[0];
            DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
            deployEvent.IsTrigger = true;
            await CardPlayPool.DoChangeEvent(deployEvent);
        })
        // Rebuilding is the other half of the removal, not an effect of its own: a skipped removal
        // finishes the card instead of granting a free piece.
        .RequiringPreviousStep()
        .WithGuidance("Select where to rebuild the US piece");
    }
}