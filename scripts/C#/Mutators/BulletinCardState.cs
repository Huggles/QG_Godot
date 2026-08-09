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
    /// <summary>
    /// Shared card face for every Bulletin, in place of a per-faction frame. Loaded lazily rather than
    /// in a static initializer so a headless server — which skips card texture loading entirely, see
    /// FactionData.LoadTextures — never touches it.
    /// </summary>
    private static Texture2D _frontTexture;
    private static Texture2D FrontTextureShared =>
        _frontTexture ??= GD.Load<Texture2D>("res://assets/textures/Other/Bulletin_Front.png");

    public BulletinCardState(CardData cardData, ActivatableMutator mutator) : base(cardData, mutator) { }

    public override Texture2D FrontTexture => FrontTextureShared;

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
}
