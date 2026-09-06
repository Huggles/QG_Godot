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

    /// <summary>
    /// The armies still awaiting relocation. Read by the removal step, its condition and
    /// <see cref="Targets"/> alike, so the three cannot disagree about who is left.
    /// </summary>
    private List<int> EligibleArmyIds =>
        GetGermanArmyIds
            .Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0)
            .ToList();

    /// <summary>Every army this will pick up, and every space it could set one down in.</summary>
    public override TargetSet Targets() =>
        TargetSet.Units(EligibleArmyIds)
            .Plus(TargetSet.Countries(CountryState.BuildableLand(Faction)));

    public override List<CardStep> OnActivate() => MakeRelocationSteps();

    private List<CardStep> MakeRelocationSteps() =>
        new List<CardStep> { MakeRemovalStep(), MakeDeployStep() };

    private CardStep MakeRemovalStep()
    {
        return new CardStep(this, async () =>
        {
            int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, EligibleArmyIds).BroadCast()).ResponseUnitIds[0];
            relocatedIds.Add(selectedUnitId);
            if (EligibleArmyIds.Count > 0) CardSteps.AddRange(MakeRelocationSteps());
            RemoveUnitChangeEvent removeEvent = BuildChangeEvent(new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
            removeEvent.IsTrigger = false;
            await CardPlayPool.DoChangeEvent(removeEvent);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
            EligibleArmyIds.Count > 0 && CountryState.BuildableLand(Faction).Count > 0), this))
        .WithGuidance("Select a German Army to eliminate and rebuild");
    }

    private CardStep MakeDeployStep()
    {
        return new CardStep(this, async () =>
        {
            await ReplayContext.Pace(1000);
            var buildableIds = CountryState.BuildableLand(Faction).Select(cs => cs.Id).ToList();
            int countryId = (await new InputRequest.SelectCountryRequestHandler(Faction, buildableIds).BroadCast()).ResponseCountryIds[0];
            DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
            deployEvent.IsTrigger = true;
            await CardPlayPool.DoChangeEvent(deployEvent);
        })
        // Rebuilding is the other half of the removal, not an effect of its own: a skipped removal
        // finishes the card instead of granting a free army.
        .RequiringPreviousStep()
        .WithGuidance("Select where to rebuild the German Army");
    }
}