using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A price a step charges for its effect: one ChangeEvent applied directly, opening no window. It is part
/// of the step it pays for, not a step of its own, so a card cancelled in that step's block window never
/// pays it. When it is paid depends on the step's kind — see <see cref="CardStep.PriceTiming"/>.
/// </summary>
public sealed class StepPrice
{
    private readonly StepChoice _choice;
    private readonly Func<Task<CardStepResult>> _produce;

    /// <summary>Whether the price can be paid now; false makes the step it belongs to unexecutable.</summary>
    internal Func<GameSituation, bool> CanPay { get; }

    /// <summary>A price with listable outcomes, so a projection can apply it.</summary>
    public StepPrice(StepChoice choice, Func<GameSituation, bool> canPay = null)
    {
        _choice = choice;
        CanPay = canPay;
    }

    /// <summary>A free-form price: a projection cannot see past it, as with a free-form step.</summary>
    public StepPrice(Func<Task<CardStepResult>> produce, Func<GameSituation, bool> canPay = null)
    {
        _produce = produce;
        CanPay = canPay;
    }

    /// <summary>Every event this price could be paid with, or null when it is free-form. The caller frees them.</summary>
    public IReadOnlyList<StepOption> Outcomes(StepContext context) => _choice?.Outcomes(context);

    // The common prices. The card's faction is read when the price is paid: steps are built before a card is dealt.

    /// <summary>"Discard the top <paramref name="count"/> cards of your draw deck."</summary>
    public static StepPrice DeckDiscard(CardLogic card, int count, Func<GameSituation, bool> canPay = null)
        => new(Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(card.Faction, card.Faction, count)), canPay);

    /// <summary>"Discard <paramref name="count"/> cards from your hand", the player's choice.</summary>
    public static StepPrice HandDiscard(CardLogic card, int count, Func<GameSituation, bool> canPay = null)
        => new(Choose.Fixed(_ => new ForceDiscardHandCardsChangeEvent(card.Faction, card.Faction, count)), canPay);

    /// <summary>Instead of playing a card from hand: spends the faction's play for the step.</summary>
    public static StepPrice SpendPlay(CardLogic card, Func<GameSituation, bool> canPay = null)
        => new(Choose.Fixed(_ => new SpendPlayActionChangeEvent(card.Faction)), canPay);

    internal async Task<CardStepResult> Produce(Faction faction)
        => _choice != null ? (await _choice.Run(faction, StepContext.Live(null))).Event : await _produce();
}
