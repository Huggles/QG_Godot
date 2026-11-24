using Godot;
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
    /// <summary>
    /// The confirm button's verb ("Discard 2 cards"), and what the hint says the cards are clicked for. Null (the
    /// default) means a discard, worded by whole translated sentences; a caller's own verb is used as given.
    /// </summary>
    public string Verb { get; }
    public string Purpose { get; }
    public List<int> Selected { get; } = new();

    public CardPick(int min, int max, string emptyLabel = null, string verb = null, string purpose = null)
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
    public string HintText
    {
        get
        {
            string count = (Min, Max) switch
            {
                _ when Min == Max => Cards(Min),
                (0, < 0) => T("HUD_PICK_ANY_CARDS"),
                (0, _) => string.Format(T("HUD_PICK_UP_TO"), Cards(Max)),
                (_, < 0) => string.Format(T("HUD_PICK_N_OR_MORE"), Min),
                _ => string.Format(T("HUD_PICK_N_TO_M"), Min, Max),
            };
            return Purpose == null ? string.Format(T("HUD_PICK_HINT_DISCARD"), count) : $"Click {count} to {Purpose}";
        }
    }

    private static string Cards(int count) => string.Format(T(count == 1 ? "HUD_PICK_CARDS_ONE" : "HUD_PICK_CARDS_OTHER"), count);

    private static string T(string key) => TranslationServer.Translate(key);

    public string ConfirmLabel => Selected.Count < Min
        ? string.Format(T("HUD_PICK_SELECT_MORE"), Min - Selected.Count)
        : Selected.Count == 0 ? EmptyLabel ?? (Verb == null ? T("HUD_PICK_DISCARD_NOTHING") : $"{Verb} nothing")
        : Verb == null
            ? string.Format(T(Selected.Count == 1 ? "HUD_PICK_DISCARD_N_ONE" : "HUD_PICK_DISCARD_N_OTHER"), Selected.Count)
            : $"{Verb} {Cards(Selected.Count)}";

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
