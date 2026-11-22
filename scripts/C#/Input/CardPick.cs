using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A multi-card pick made straight in the hand fan (a discard, Rosie the Riveter): the bounds, the confirm wording, and
/// the cards picked so far. Lives on <see cref="ActiveCardPrompt.Pick"/> so a redraw keeps the picks.
/// </summary>
public sealed class CardPick
{
    public int Min { get; }
    /// <summary>-1 for no upper bound.</summary>
    public int Max { get; }
    /// <summary>The confirm label while nothing is picked, e.g. naming the Total War discard-nothing VP.</summary>
    public string EmptyLabel { get; }
    /// <summary>The confirm button's verb ("Discard 2 cards"), and what the hint says the cards are clicked for.</summary>
    public string Verb { get; }
    public string Purpose { get; }
    public List<int> Selected { get; } = new();

    public CardPick(int min, int max, string emptyLabel = null, string verb = "Discard", string purpose = "discard")
    {
        Min = min;
        Max = max;
        EmptyLabel = emptyLabel;
        Verb = verb;
        Purpose = purpose;
    }

    public static CardPick Exactly(int count) => new(count, count);

    public bool IsFull => Max >= 0 && Selected.Count >= Max;
    public bool CanConfirm => Selected.Count >= Min && (Max < 0 || Selected.Count <= Max);

    /// <summary>The line above the confirm button, naming how many cards the pick wants.</summary>
    public string HintText => $"Click {(Min, Max) switch
    {
        _ when Min == Max => Cards(Min),
        (0, < 0) => "any cards",
        (0, _) => $"up to {Cards(Max)}",
        (_, < 0) => $"{Min} or more cards",
        _ => $"{Min} to {Max} cards",
    }} to {Purpose}";

    private static string Cards(int count) => $"{count} {(count == 1 ? "card" : "cards")}";

    public string ConfirmLabel => Selected.Count < Min
        ? $"Select {Min - Selected.Count} more"
        : Selected.Count == 0 ? EmptyLabel ?? $"{Verb} nothing"
        : $"{Verb} {Selected.Count} {(Selected.Count == 1 ? "card" : "cards")}";

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
