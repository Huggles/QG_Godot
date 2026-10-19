using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// "Use when you are the target of an Economic Warfare card. Remove your Air Force from the board instead of
/// discarding cards." Offered in the block window of an Allied EW card's discard against Italy, while the
/// Italian Air Force is on the board: it is removed, and the discard blocked.
/// </summary>
public partial class BolsterRegiaAeronautica : BolsterCardLogic
{
    private int ItalianAirForce(BoardState board) => board.ActiveAirForceIds(Faction).FirstOrDefault(-1);

    private bool IsDiscardAgainstItaly(ChangeEvent trigger) =>
        trigger is ForceDiscardCardsChangeEvent or ForceDiscardHandCardsChangeEvent && trigger.TargetFaction == Faction;

    public override TargetSet Targets() => ItalianAirForce(BoardState.Live) is var airForce && airForce >= 0
        ? TargetSet.Units(new List<int> { airForce })
        : TargetSet.None;

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsBlockRequest(CardType.ECONOMIC_WARFARE), this),
        Condition.Build(new Condition.CustomCondition(s => IsDiscardAgainstItaly(s.BlockTrigger) && ItalianAirForce(s.Board) >= 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(c => new RemoveUnitChangeEvent(Faction, ItalianAirForce(c.Board), UnitRemovalReason.ELIMINATE, c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => ItalianAirForce(s.Board) >= 0), this))
            .WithGuidance("Remove the Italian Air Force instead of discarding cards"),
        new BlockStep<ChangeEvent>(this, async _ => {
            await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} uses Regia Aeronautica: its Air Force is removed instead of the cards").Apply();
            await Task.Delay(GameSettings.DurationMedium);
            return CardStepResult.Block();
        })
        .RequiringPreviousStep()
        .WithGuidance("Discard nothing")
    };
}
