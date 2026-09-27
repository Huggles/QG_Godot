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

    /// <summary>
    /// The armies still awaiting relocation. Read by the removal step, its condition and
    /// <see cref="Targets"/> alike, so the three cannot disagree about who is left.
    /// </summary>
    private List<int> EligibleArmyIds =>
        GetSovietArmyIds
            .Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id)?.CountryId ?? -1) >= 0)
            .ToList();

    /// <summary>Every army this will pick up, and every space it could set one down in.</summary>
    public override TargetSet Targets() =>
        TargetSet.Units(EligibleArmyIds)
            .Plus(TargetSet.Countries(CountryState.BuildableLand(Faction)));

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
        return new RequirementStep(this, Choose.UnitFrom(() => EligibleArmyIds,
                unitId => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE))
            .OnChosen(chosen =>
            {
                relocatedIds.Add(chosen.Value.Id);
                if (EligibleArmyIds.Count > 0) CardSteps.AddRange(MakeRelocationSteps());
            }))
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
            EligibleArmyIds.Count > 0 && CountryState.BuildableLand(Faction).Count > 0), this))
        .WithGuidance("Select a Soviet Army to eliminate and rebuild");
    }

    private CardStep MakeDeployStep() {
        return new ResultStep(this, Choose.CountryFrom(() => CountryState.BuildableLand(Faction).Select(cs => cs.Id).ToList(),
                countryId => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD))
            .BeforePrompt(() => ReplayContext.Pace(1000)))
        // Rebuilding is the other half of the removal, not an effect of its own: a skipped removal
        // finishes the card instead of granting a free army.
        .RequiringPreviousStep()
        .WithGuidance("Select where to rebuild the Soviet Army");
    }
}