using System.Threading.Tasks;

/// <summary>
/// Example scenario mutator: "during the victory step, score additional points".
///
/// It emits its OWN ScorePointsChangeEvent rather than appending to the step's VPTurnSummary —
/// VictoryStepHandlerDefault.ProcessVictoryStep has already applied its summary by the time an AFTER
/// hook runs. GameFlow.VictoryPointSummaries groups by round, so several summaries per round is fine.
/// </summary>
public class MutatorBonusVictoryPoints : StepMutator
{
    private const int BonusPoints = 2;

    public override TurnStep      Step        => TurnStep.VICTORY_POINT;
    public override MutatorTiming Timing      => MutatorTiming.AFTER;
    public override string        Description => "Scenario bonus victory points";

    public override Task Run(Faction activeFaction) =>
        this.Do(new ScorePointsChangeEvent(new VPEntry(BonusPoints, "scenario bonus"), activeFaction));
}
