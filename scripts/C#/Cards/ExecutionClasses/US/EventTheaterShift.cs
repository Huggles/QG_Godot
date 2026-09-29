using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class EventTheaterShift : EventCardLogic
{
    private List<int> GetUSUnitIds(BoardState board) =>
        board.ActiveUnitIds(Faction)
            .Where(uId => UnitState.ForId(uId) != null)
            .ToList();

    private List<int> relocatedIds = new List<int>();

    /// <summary>
    /// The pieces still awaiting relocation. Read by the removal step, its condition and
    /// <see cref="Targets"/> alike, so the three cannot disagree about who is left.
    /// </summary>
    private List<int> EligibleUnitIds(BoardState board) =>
        GetUSUnitIds(board)
            .Where(id => !relocatedIds.Contains(id) && (UnitState.ForId(id) is { } unit ? board.CountryOf(unit) : -1) >= 0)
            .ToList();

    /// <summary>Every piece this will pick up, and every space it could set one down in. Both types,
    /// since which one the rebuild offers depends on the piece the player picks first.</summary>
    public override TargetSet Targets() =>
        TargetSet.Units(EligibleUnitIds(BoardState.Live))
            .Plus(TargetSet.Countries(BoardState.Live.BuildableLand(Faction)))
            .Plus(TargetSet.Countries(BoardState.Live.BuildableSea(Faction)));

    // The rebuild offers the removed piece's own type. Read off the removal step's outcome, which is
    // always there on the real path: the rebuild requires the removal to have happened.
    private static bool RemovedNavy(StepOption removal) => UnitState.ForId(removal.Target.Value.Id).Type == UnitType.NAVY;

    public override List<CardStep> OnActivate() => MakeRelocationSteps();

    private List<CardStep> MakeRelocationSteps() =>
        new List<CardStep> { MakeRemovalStep(), MakeDeployStep() };

    private CardStep MakeRemovalStep()
    {
        return new RequirementStep(this, Choose.UnitFrom(c => EligibleUnitIds(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board))
            .OnChosen(chosen =>
            {
                relocatedIds.Add(chosen.Value.Id);
                if (EligibleUnitIds(BoardState.Live).Count > 0) CardSteps.AddRange(MakeRelocationSteps());
            }))
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
            EligibleUnitIds(s.Board).Count > 0
            && (s.Board.BuildableLand(Faction).Count > 0 || s.Board.BuildableSea(Faction).Count > 0)), this))
        .WithGuidance("Select a US Army or Navy to eliminate and rebuild");
    }

    private CardStep MakeDeployStep()
    {
        return new ResultStep(this, Choose.CountryFrom(c => RemovedNavy(c.Previous.Value)
                    ? c.Board.BuildableSea(Faction).Select(cs => cs.Id).ToList()
                    : c.Board.BuildableLand(Faction).Select(cs => cs.Id).ToList(),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD))
            .BeforePrompt(() => ReplayContext.Pace(1000)))
        // Rebuilding is the other half of the removal, not an effect of its own: a skipped removal
        // finishes the card instead of granting a free piece.
        .RequiringPreviousStep()
        .WithGuidance("Select where to rebuild the US piece");
    }
}