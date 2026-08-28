/// <summary>
/// Marker for a modifier that a save-game replay cannot reconstruct, because it was registered from
/// inside a <c>CardStep</c> rather than by a <c>ChangeEvent</c>.
///
/// A save is the ChangeEvent log, replayed. Anything a card's step logic does directly — as opposed to
/// through an event — leaves no trace in that log and simply will not exist in the restored game. Nearly
/// nothing does this: a status card's own modifier is registered by <c>DeckState.PlayCard</c>, which
/// <c>PlayCardChangeEvent</c> drives, so replay re-registers it for free. The exception is
/// <see cref="MutatorRecycleAfterStep"/>, which the reacting card registers as a detached instance.
///
/// Rather than teach the save format about each such case, <c>GameFlow.CanSave</c> refuses to save while
/// one is registered. That costs nothing in practice: these are one-shots that fire at the end of the
/// turn step they were created in, and both save checkpoints already fall outside that window. The
/// marker exists so that a THIRD such modifier, added later by someone who has never read this, is
/// caught by the save gate instead of silently vanishing on reload.
///
/// If you find yourself wanting to save while one of these is live, the fix is to give the modifier a
/// ChangeEvent of its own, not to relax the gate.
/// </summary>
public interface IUnsavedModifier : IModifier
{
}
