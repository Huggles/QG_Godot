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
/// <param name="SeparateNonHandCards">
/// Presentation only: whether this prompt draws the cards that are not in hand as their own smaller
/// fan. Carried so a re-draw after browsing reproduces the prompt as the player last saw it, rather
/// than collapsing it back into one row.
/// </param>
/// <param name="PreviewsByCardId">
/// What each drawn card could affect, for the hover preview — see
/// <see cref="CardTargetPreviewDisplay"/>. Computed by the host and shipped on
/// <see cref="InputRequest.CardTargetPreviews"/>; a client cannot re-derive it, for the same reasons
/// it cannot re-derive <paramref name="SelectableCardIds"/>. A card absent from the map has nothing
/// to show.
///
/// Holds the wire entry whole rather than one dictionary per target kind. Countries and units are
/// already two lists and a third kind would be a third dictionary here, a third parameter, and a
/// third thing for every caller to thread through — the same reasoning that makes
/// <see cref="TargetSet"/> a list of <see cref="TargetRef"/> instead of a field per kind.
/// </param>
public sealed record ActiveCardPrompt(
    Faction Faction,
    List<int> DisplayCardIds,
    List<int> SelectableCardIds,
    bool SeparateNonHandCards = false,
    Dictionary<int, InputRequest.CardTargetPreview> PreviewsByCardId = null);
