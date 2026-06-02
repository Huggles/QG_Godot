using Godot;

/// <summary>
/// Implemented by status cards that passively modify a ForceDiscardCardsChangeEvent
/// before it executes. Modifiers are applied automatically — no player activation needed.
/// </summary>
public interface IDiscardModifier
{
    /// <summary>
    /// Return the delta to apply to NumberOfCards (negative = reduce, positive = increase).
    /// Return 0 if this modifier does not apply to the given event.
    /// </summary>
    int ModifyDiscard(ForceDiscardCardsChangeEvent discardEvent);
}
