using Godot;

/// <summary>
/// A Bulletin — the player-facing form of an <see cref="ActivatableMutator"/>. It is presented as a
/// card and activated like one, but it is owned by the scenario rather than by a deck: it has no entry
/// in QGData_Cards_V2.json, it is in NO DeckState pile, and it is never discarded. Each eligible
/// faction gets its own Bulletin for a given mutator, which is why it still carries a real Faction.
///
/// Created only by RegisterBulletinCardChangeEvent, which runs on every peer so host and clients agree
/// on ids. DeckState.PlayCard and DeckState.DiscardCard are unreachable for an id that is in no pile,
/// and ActivateReactionChangeEvent only discards RESPONSE cards, so activating a Bulletin never moves
/// it anywhere.
/// </summary>
public partial class BulletinCardState : CardState
{
    public BulletinCardState(CardData cardData, ActivatableMutator mutator) : base(cardData, mutator) { }

    /// <summary>
    /// The shared Bulletin face in place of a per-faction frame — the same art an automatic step
    /// mutator draws through CardFace.Bulletin, which has no CardState to hang it off.
    /// </summary>
    public override Texture2D FrontTexture => BulletinArt.Front;

    /// <summary>
    /// Permanently on the table. Tag.IsPlayed is derived from the DeckState piles by
    /// GameStateCalculator.CalculatePlayedCardsForFaction, which can never see a pile-less card — and
    /// "played" is what makes CardLogic._conditions use CardTriggers() instead of _defaultPlayConditions,
    /// and what CalculateAfterReactionCardsForFaction requires before offering a reaction.
    /// </summary>
    public override bool IsPlayed => true;

    /// <summary>
    /// Never in a discard pile. The base implementation would ask DeckState for a pile that never
    /// contains this id; overriding states the intent rather than relying on that.
    /// </summary>
    public override bool IsDiscarded => false;

    /// <summary>
    /// The mutator's text as it reads right now, not the copy RegisterBulletinCardChangeEvent baked
    /// into the synthetic CardData at setup. A mutator whose Text depends on game state — see
    /// MutatorReallocateResources, which names the cards still in the draw deck — would otherwise
    /// render frozen at its round-1 value for the whole game.
    ///
    /// Falls back to CardData.Text: CardLogic is null on a card whose ExecutionClass failed to
    /// resolve, and the base class handles that case everywhere else too.
    /// </summary>
    public override string DisplayText => (CardLogic as ActivatableMutator)?.Text ?? CardData.Text;
}
