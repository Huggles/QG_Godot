using System.Threading.Tasks;

/// <summary>
/// One-shot: moves a card out of its faction's discard pile back into the draw deck once the given
/// turn step ends. The destination is the caller's — ResponseRationing shuffles it in,
/// StatusWomenConscripts puts it on top.
///
/// Registered by the reacting card as a detached instance rather than implemented on the card itself.
/// Two reasons: DeckState.PlayCard only registers modifiers for STATUS cards, so a Response card's
/// logic would never be registered; and a detached instance is untouched by wherever the reacting
/// card ends up (response pile, discard pile, recycled).
///
/// The deferral is the point. Both cards fire in the played card's introduction window, before that
/// card's own CardSteps have run — recycling immediately would put it back in the deck and only then
/// resolve its effect, with Tag.IsPlayed no longer set and the card redrawable in the same turn.
/// Their text is about where the played card ENDS UP, not whether it resolves, so waiting for the
/// step to end is what the cards actually say. Waiting also lets the CardPlayRound finish normally.
/// </summary>
public class MutatorRecycleAfterStep : StepMutator
{
    private readonly Faction _faction;

    /// <summary>The card this mutator moves — the one that was played, not the one that reacted.</summary>
    private readonly int _recycledCardId;

    /// <summary>
    /// The reacting card that registered this mutator (Rationing, Women Conscripts). Deliberately a
    /// second id: it is what the mutator announces itself with when it fires, and confusing it with
    /// <see cref="_recycledCardId"/> would show the player the wrong card entirely.
    /// </summary>
    private readonly int _sourceCardId;

    private readonly TurnStep _step;
    private readonly RecycleDestination _destination;
    private readonly string _description;
    private readonly string _bulletinText;
    private readonly int _turn;
    private bool _done;

    public MutatorRecycleAfterStep(
        Faction faction,
        int recycledCardId,
        int sourceCardId,
        TurnStep step,
        RecycleDestination destination,
        string description,
        string bulletinText)
    {
        _faction = faction;
        _recycledCardId = recycledCardId;
        _sourceCardId = sourceCardId;
        _step = step;
        _destination = destination;
        _description = description;
        _bulletinText = bulletinText;
        _turn = GameFlow.Instance.GameTurn;
    }

    // Captured at registration, so this also works if the card is ever activated in the START step.
    public override TurnStep      Step         => _step;
    public override MutatorTiming Timing       => MutatorTiming.AFTER;
    public override string        Description  => _description;
    public override string        BulletinText => _bulletinText;
    public override int           SourceCardId => _sourceCardId;

    /// <summary>
    /// Retires once fired. The turn check is the backstop for a step abandoned by error recovery —
    /// without it a mutator whose window never arrived would linger and fire on a later turn.
    /// </summary>
    public override bool IsExpired => _done || GameFlow.Instance.GameTurn > _turn;

    public override bool ShouldRun(Faction activeFaction) =>
        activeFaction == _faction
        && DeckState.ForFaction(_faction).DiscardedCardIds.Contains(_recycledCardId);

    public override async Task Run(Faction activeFaction)
    {
        _done = true;
        // isTrigger:false — a bookkeeping move, as it was before. No block or reaction window.
        await this.Do(
            new RecycleCardChangeEvent(_faction, _faction, _recycledCardId, _destination),
            isTrigger: false);
    }
}
