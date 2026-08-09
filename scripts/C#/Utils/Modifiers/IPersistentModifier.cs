/// <summary>
/// Marker for modifiers that must survive their source card leaving the table — the "played once,
/// effect lasts the rest of the game" case. DeckState.DiscardCard skips unregistering these.
///
/// A persistent modifier ends only via its own expiry (see IStepMutator.IsExpired) or when
/// ModifierRegistry.Clear() runs at the start of a new game. Without this marker a status card's
/// modifier is unregistered the moment the card is discarded, which is the default and stays so.
/// </summary>
public interface IPersistentModifier : IModifier
{
}
