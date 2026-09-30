using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class EventTransSiberianRailroad : EventCardLogic
{
    private List<int> GetSovietArmyIds(BoardState board) =>
        board.ActiveUnitIds(Faction)
            .Where(uId => UnitState.ForId(uId).IsArmy)
            .ToList();

    private List<int> relocatedIds = new List<int>();

    /// <summary>
    /// The armies still awaiting relocation. Read by the removal step, its condition and
    /// <see cref="Targets"/> alike, so the three cannot disagree about who is left.
    /// </summary>
    private List<int> EligibleArmyIds(BoardState board) =>
        GetSovietArmyIds(board)
            .Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id) is { } unit ? board.CountryOf(unit) : -1) >= 0)
            .ToList();

    /// <summary>Every army this will pick up, and every space it could set one down in.</summary>
    public override TargetSet Targets() =>
        TargetSet.Units(EligibleArmyIds(BoardState.Live))
            .Plus(TargetSet.Countries(BoardState.Live.BuildableLand(Faction)));

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
        return new RequirementStep(this, Choose.UnitFrom(c => EligibleArmyIds(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board))
            .OnChosen(chosen =>
            {
                relocatedIds.Add(chosen.Value.Id);
                if (EligibleArmyIds(BoardState.Live).Count > 0) CardSteps.AddRange(MakeRelocationSteps());
            }))
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
            EligibleArmyIds(s.Board).Count > 0 && s.Board.BuildableLand(Faction).Count > 0), this))
        .WithGuidance("Select a Soviet Army to eliminate and rebuild");
    }

    private CardStep MakeDeployStep() {
        return new ResultStep(this, Choose.CountryFrom(c => c.Board.BuildableLand(Faction).Select(cs => cs.Id).ToList(),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD))
            .BeforePrompt(() => ReplayContext.Pace(1000)))
        // Rebuilding is the other half of the removal, not an effect of its own: a skipped removal
        // finishes the card instead of granting a free army.
        .RequiringPreviousStep()
        .WithGuidance("Select where to rebuild the Soviet Army");
    }
}