using System;
using System.Threading.Tasks;

/// <summary>
/// A nested card play: EventLendLease (an ally plays a card out of turn), EventFlexibleResources and
/// StatusGuards (play a card fished out of the discard pile), MutatorReallocateResources.
///
/// The body chooses the card and returns <see cref="CardStepResult.PlayCard"/>; going through
/// <c>CardPlayPool.DoCard</c> rather than reproducing the effect is what makes the played card a
/// real play — it emits a PlayCardChangeEvent, so Condition.FactionPlayedCard fires and the Kind B
/// reactions (Rationing, Women Conscripts) are offered against it.
///
/// Returning <see cref="CardStepResult.Nothing"/> is the "declined" answer and must not be an
/// exception: EventLendLease's own draw is a separate step, and an ally who declines a gift should
/// not cost the US its card.
/// </summary>
public sealed partial class PlayCardStep : CardStep
{
    private readonly Func<Task<CardStepResult>> _chooseCard;

    public PlayCardStep(CardLogic cardLogic, Func<Task<CardStepResult>> chooseCard) : base(cardLogic)
        => _chooseCard = chooseCard;

    public override StepKind Kind => StepKind.PlayCard;

    /// <summary>None: the nested DoCard recalculates on each of the played card's own steps.</summary>
    public override RecalcScope RecalcScope => RecalcScope.None;

    protected override Task<CardStepResult> RunCoreAsync() => _chooseCard();

    protected override async Task DispatchAsync(CardStepResult result)
    {
        switch (result.Outcome)
        {
            case StepOutcome.Nothing:
                break;
            case StepOutcome.PlayCard:
                await CardPlayPool.DoCard(result.PlayCardId);
                break;
            default:
                ReportWrongArm(result);
                break;
        }
    }
}
