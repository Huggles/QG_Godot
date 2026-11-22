using System;
using System.Threading.Tasks;

/// <summary>
/// The common case: one ChangeEvent that IS the card's effect. Offered for block, applied, then an
/// after-reaction window — the full <c>CardPlayRound.DoChangeEvent</c> pipeline.
///
/// <c>IsTrigger = true</c> comes from the type, which matters more than it looks: per the
/// card-reaction-system skill, a card whose first step's only event is <c>IsTrigger = false</c>
/// silently offers no Kind A or Kind B reaction to being played at all, and nothing reports it.
/// That was a per-card hand-written line; it is now a consequence of choosing this type.
/// </summary>
public sealed partial class ResultStep : EventCardStep
{
    public ResultStep(CardLogic cardLogic, Func<Task<CardStepResult>> produce) : base(cardLogic, produce) { }

    public ResultStep(CardLogic cardLogic, StepChoice choice) : base(cardLogic, choice) { }

    public override StepKind Kind => StepKind.Result;

    protected override bool EventIsTrigger => true;

    /// <summary>The price is paid inside DoChangeEvent, after the block window, so a cancelled card never pays it.</summary>
    protected internal override PriceTiming PriceTiming => PriceTiming.InDispatch;

    protected override Task DispatchEventAsync(ChangeEvent changeEvent)
        => CardPlayPool.DoChangeEvent(changeEvent, Prices == null ? null : PayPricesAsync);
}
