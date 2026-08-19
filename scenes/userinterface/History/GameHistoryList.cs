using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The game history strip: a bottom-aligned column of <see cref="GameHistoryItem"/> badges, newest at
/// the bottom, capped at <see cref="MaxItems"/>.
///
/// No ScrollContainer and no fading. With a hard cap the content can never overflow the strip, which
/// is what lets the whole thing stay on screen permanently — the badges are narrow enough to cost
/// almost nothing, so there is nothing to hide.
/// </summary>
public partial class GameHistoryList : VBoxContainer
{
    /// <summary>
    /// How many badges the strip keeps. Must satisfy <c>MaxItems * row height &lt;= strip height</c>
    /// (30px rows against the 630px strip laid out in user_interface.tscn: offset_top 200,
    /// offset_bottom -250 of 1080). Raise it past that and the oldest badges get silently clipped by
    /// clip_contents instead of freed, which looks like a bug rather than a cap.
    /// </summary>
    private const int MaxItems = 20;

    private List<int> DisplayedChangeEventIds = new(); // to prevent duplicates when joining mid-game

    /// <summary>
    /// The number shown on the next badge. Its own counter rather than GameMessage.Id, which counts
    /// every message on the channel — including the ones that never become history — so the badges
    /// would read 3, 7, 8, 14. Not derived from the child count either, because trimming must not
    /// renumber the entries still on screen.
    /// </summary>
    private int nextSequence = 1;

    /// <summary>
    /// Whether this is the locally-controlled copy. user_interface.tscn is instanced once per
    /// PlayerScene, each with its own multiplayer authority, and only the local one draws history.
    /// </summary>
    private bool isLocalList;

    public override void _Ready()
    {
        // RemoveChild before QueueFree: a queued child still counts in GetChildren() this frame, so
        // leaving the design-time placeholders in place would make TrimToWindow free the real badges
        // it had just added.
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        isLocalList = Multiplayer.GetUniqueId() == GetMultiplayerAuthority();
        if (!isLocalList) return;

        AddUnprocessedChangeEvents();
        EventBus.Instance.GameChangeEventAfter += OnGameChangeEventApplied;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (isLocalList && EventBus.Instance != null)
        {
            EventBus.Instance.GameChangeEventAfter -= OnGameChangeEventApplied;
        }
    }

    private void AddUnprocessedChangeEvents()
    {
        foreach (GameMessage message in GetUnprocessedChangeEvents())
        {
            AddChild(GameHistoryItem.Create(GameHistoryEntry.For(message, nextSequence)));
            nextSequence++;
        }
        TrimToWindow();
    }

    private void OnGameChangeEventApplied(string changeEventName)
    {
        AddUnprocessedChangeEvents();
    }

    /// <summary>
    /// Drop the oldest badges back down to <see cref="MaxItems"/>. RemoveChild is immediate, so a
    /// second call in the same frame counts correctly; the freed item's _ExitTree releases the hover
    /// popup if that badge happened to own it.
    /// </summary>
    private void TrimToWindow()
    {
        List<Node> items = GetChildren().ToList();
        for (int i = 0; i < items.Count - MaxItems; i++)
        {
            RemoveChild(items[i]);
            items[i].QueueFree();
        }
    }

    private List<GameMessage> GetUnprocessedChangeEvents()
    {
        List<GameMessage> unprocessed = new();
        foreach (GameMessage ev in MultiplayerSession.Instance.GameState.GameMessages)
        {
            if (!DisplayedChangeEventIds.Contains(ev.Id) && ev.ToHistoryItem)
            {
                unprocessed.Add(ev);
                DisplayedChangeEventIds.Add(ev.Id);
            }
        }
        return unprocessed;
    }
}
