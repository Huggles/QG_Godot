using System.Collections.Generic;

/// <summary>
/// The card prompt currently open on this peer, as <see cref="InputManager.SetCardSelectionActive"/>
/// received it. Held by <see cref="InputManager.CurrentCardPrompt"/> so the prompt can be re-drawn
/// after the player has browsed something else on the hand display.
/// </summary>
/// <param name="Faction">The faction being asked to choose.</param>
/// <param name="DisplayCardIds">Everything the prompt draws, including the greyed-out cards.</param>
/// <param name="SelectableCardIds">
/// The subset that may actually be clicked. Only the host can compute this — see the remarks on
/// <see cref="InputManager.SetCardSelectionActive"/> — so it must be carried around, never re-derived.
/// </param>
public sealed record ActiveCardPrompt(
    Faction Faction,
    List<int> DisplayCardIds,
    List<int> SelectableCardIds);
