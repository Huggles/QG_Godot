using System.Threading.Tasks;

/// <summary>
/// One-shot: shuffles a card out of its faction's discard pile back into the draw deck once the given
/// turn step ends.
///
/// Registered by ResponseRationing as a detached instance rather than implemented on the card itself.
/// Two reasons: DeckState.PlayCard only registers modifiers for STATUS cards, so a Response card's
/// logic would never be registered; and a detached instance is untouched by wherever the Rationing
/// card ends up (response pile, discard pile, recycled).
///
/// The deferral is the point. Rationing fires in the activation window, before the triggering card's
/// own CardSteps have run — recycling immediately would put the card back in the deck and only then
/// resolve its effect. Waiting for the step to end lets the CardPlayRound finish normally.
/// </summary>
public class MutatorRecycleAfterStep : StepMutator
{
    private readonly Faction _faction;
    private readonly int _cardId;
    private readonly TurnStep _step;
    private readonly int _turn;
    private bool _done;

    public MutatorRecycleAfterStep(Faction faction, int cardId, TurnStep step)
    {
        _faction = faction;
        _cardId = cardId;
        _step = step;
        _turn = GameFlow.Instance.GameTurn;
    }

    // Captured at registration, so this also works if the card is ever activated in the START step.
    public override TurnStep      Step        => _step;
    public override MutatorTiming Timing      => MutatorTiming.AFTER;
    public override string        Description => "Rationing: shuffle the played card into the draw deck";

    /// <summary>
    /// Retires once fired. The turn check is the backstop for a step abandoned by error recovery —
    /// without it a mutator whose window never arrived would linger and fire on a later turn.
    /// </summary>
    public override bool IsExpired => _done || GameFlow.Instance.GameTurn > _turn;

    public override bool ShouldRun(Faction activeFaction) =>
        activeFaction == _faction
        && DeckState.ForFaction(_faction).DiscardedCardIds.Contains(_cardId);

    public override async Task Run(Faction activeFaction)
    {
        _done = true;
        // isTrigger:false — a bookkeeping move, as it was before. No block or reaction window.
        await this.Do(
            new RecycleCardChangeEvent(_faction, _faction, _cardId, RecycleDestination.ShuffleIntoDeck),
            isTrigger: false);
    }
}
