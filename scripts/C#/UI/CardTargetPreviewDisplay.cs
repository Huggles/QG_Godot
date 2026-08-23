using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Lights up what a card could affect while the player hovers it in an open card prompt, so the
/// targets are visible before the card is committed to rather than one click later.
///
/// Countries and units are lit separately, because they are drawn differently: a country glows, a
/// unit puts up its own target marker. A card that reaches one army in Egypt lights that army, not
/// the whole country — which is what makes a battle card's preview worth reading at all.
///
/// Static rather than a node: it owns no scene of its own. The visual belongs to each
/// <see cref="CountryScene"/> and <see cref="UnitScene"/>, reached by raising
/// <see cref="Tag.PreviewTarget"/> — the same indirection <see cref="SelectCountryHandler"/> uses with
/// <see cref="Tag.Clickable"/>, and the reason this needs no reference to the board at all.
///
/// The target sets are NOT computed here. They come from the host on
/// <see cref="InputRequest.CardTargetPreviews"/> and are held on
/// <see cref="InputManager.CurrentCardPrompt"/>; a client holds no CardPlayRound and could not
/// derive them.
/// </summary>
public static class CardTargetPreviewDisplay
{
    /// <summary>
    /// The card whose targets are currently lit, or -1 for none.
    ///
    /// Godot does not guarantee MouseExited(A) fires before MouseEntered(B) within one motion event,
    /// so moving quickly along the hand can deliver the exit for the card just left AFTER the enter
    /// for the card just reached. <see cref="Clear"/> therefore only acts when the caller still owns
    /// the preview — the ownership-token pattern HistoryDetailPopup uses for the same reason.
    /// </summary>
    private static int owningCardId = -1;

    /// <summary>
    /// Exactly the countries this class raised the tag on, so teardown cannot clear a tag somebody
    /// else raised or miss one whose target set has since changed.
    /// </summary>
    private static List<int> litCountryIds = new();

    /// <inheritdoc cref="litCountryIds"/>
    private static List<int> litUnitIds = new();

    /// <summary>
    /// Light up what <paramref name="cardId"/> could reach. A no-op when no card prompt is open —
    /// hovering a card outside a prompt (browsing another faction's hand, the history popup) must not
    /// paint the board, and there is no host-computed preview to paint anyway.
    /// </summary>
    public static void Show(int cardId)
    {
        if (cardId < 0) return;

        ActiveCardPrompt prompt = InputManager.CurrentCardPrompt;
        if (prompt?.PreviewsByCardId == null) return;

        // Cleared before the early return as well as after it: the card being hovered owns the board
        // either way, and a card that reaches nothing must take the previous card's lights down with it.
        ClearAll();
        owningCardId = cardId;

        if (!prompt.PreviewsByCardId.TryGetValue(cardId, out InputRequest.CardTargetPreview preview)
            || preview == null || preview.IsEmpty)
        {
            // The card is in the prompt but reaches nothing — a Status card with no declared preview,
            // or a card whose targets have all gone. An empty board IS the answer for a card that
            // cannot do anything right now.
            return;
        }

        // Resolved and filtered rather than passed straight to AddTag: ForIds yields a null for an id
        // it does not know, and the tag extensions dereference every element. These ids arrived over
        // the wire, so an unresolvable one must cost the preview and not the prompt.
        List<CountryState> countryStates = CountryState.ForIds((preview.CountryIds ?? new List<int>()).Distinct())
            .Where(countryState => countryState != null)
            .ToList();

        List<UnitState> unitStates = UnitState.ForIds((preview.UnitIds ?? new List<int>()).Distinct())
            .Where(unitState => unitState != null)
            .ToList();

        litCountryIds = countryStates.Select(countryState => countryState.Id).ToList();
        litUnitIds = unitStates.Select(unitState => unitState.Id).ToList();

        countryStates.AddTag(Tag.PreviewTarget, Faction.ALL);
        unitStates.AddTag(Tag.PreviewTarget, Faction.ALL);
    }

    /// <summary>
    /// Clear the preview, but only if <paramref name="cardId"/> is still the card that owns it. Call
    /// this from a card's MouseExited; a stale exit arriving after the next card's enter is dropped.
    /// </summary>
    public static void Clear(int cardId)
    {
        if (cardId != owningCardId) return;
        ClearAll();
    }

    /// <summary>
    /// Clear unconditionally, whoever owns it. For teardown paths where the prompt itself is going
    /// away and no MouseExited can be relied on: the prompt being answered, the hand being re-drawn
    /// (which frees the CardScene nodes outright), a fresh prompt opening over this one.
    /// </summary>
    public static void ClearAll()
    {
        if (litCountryIds.Count > 0)
        {
            CountryState.ForIds(litCountryIds)
                .Where(countryState => countryState != null)
                .ToList()
                .RemoveTag(Tag.PreviewTarget, Faction.ALL);
            litCountryIds = new List<int>();
        }

        if (litUnitIds.Count > 0)
        {
            UnitState.ForIds(litUnitIds)
                .Where(unitState => unitState != null)
                .ToList()
                .RemoveTag(Tag.PreviewTarget, Faction.ALL);
            litUnitIds = new List<int>();
        }

        owningCardId = -1;
    }
}
