using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A multi-card pick made straight in the hand fan (a discard): the bounds, the confirm wording, and
/// the cards picked so far. Lives on <see cref="ActiveCardPrompt.Pick"/> so a redraw keeps the picks.
/// </summary>
public sealed class CardPick
{
    public int Min { get; }
    /// <summary>-1 for no upper bound.</summary>
    public int Max { get; }
    /// <summary>The confirm label while nothing is picked, e.g. naming the Total War discard-nothing VP.</summary>
    public string EmptyLabel { get; }
    public List<int> Selected { get; } = new();

    public CardPick(int min, int max, string emptyLabel = null)
    {
        Min = min;
        Max = max;
        EmptyLabel = emptyLabel;
    }

    public static CardPick Exactly(int count) => new(count, count);

    public bool IsFull => Max >= 0 && Selected.Count >= Max;
    public bool CanConfirm => Selected.Count >= Min && (Max < 0 || Selected.Count <= Max);

    /// <summary>The line above the confirm button, naming how many cards the pick wants.</summary>
    public string HintText => Min == Max
        ? $"Click {Min} {(Min == 1 ? "card" : "cards")} to discard"
        : Max == 1 ? "Click a card to discard"
        : $"Click {System.Math.Max(Min, 1)} or more cards to discard";

    public string ConfirmLabel => Selected.Count < Min
        ? $"Select {Min - Selected.Count} more"
        : Selected.Count == 0 ? EmptyLabel ?? "Discard nothing"
        : $"Discard {Selected.Count} {(Selected.Count == 1 ? "card" : "cards")}";

    /// <summary>
    /// Picks or unpicks <paramref name="cardId"/> and returns every card whose state changed. A full
    /// single pick swaps; a full multi pick ignores the click rather than guess which card to drop.
    /// </summary>
    public List<int> Toggle(int cardId)
    {
        if (Selected.Remove(cardId)) return new List<int> { cardId };
        if (!IsFull)
        {
            Selected.Add(cardId);
            return new List<int> { cardId };
        }
        if (Max != 1) return new List<int>();

        int dropped = Selected[0];
        Selected.Clear();
        Selected.Add(cardId);
        return new List<int> { dropped, cardId };
    }

    /// <summary>Drops picks the redrawn fan no longer shows, so a card that left the hand cannot be confirmed.</summary>
    public void KeepOnly(IEnumerable<int> shownCardIds)
    {
        HashSet<int> shown = shownCardIds.ToHashSet();
        Selected.RemoveAll(cardId => !shown.Contains(cardId));
    }
}
