using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Static proxy that delegates to <see cref="CardPlayRound.Current"/>.
/// Kept so existing call-sites in ChangeEvent, Conditions, CardLogic, etc. continue to compile
/// without modification.
/// </summary>
public static class CardPlayPool
{
    // ── State pass-through ─────────────────────────────────────────────────────
    public static List<CardState> CardPool =>
        CardPlayRound.Current?.CardPool ?? new();

    public static List<ChangeEvent> ChangeEventsPool =>
        CardPlayRound.Current?.ChangeEventsPool ?? new();

    public static ChangeEvent LastChangeEvent
    {
        get => CardPlayRound.Current?.LastChangeEvent;
        set { if (CardPlayRound.Current != null) CardPlayRound.Current.LastChangeEvent = value; }
    }

    public static Dictionary<int, CardState> CardPoolMap =>
        CardPlayRound.Current?.CardPoolMap ?? new();

    public static Dictionary<int, ChangeEvent> ChangeEventsPoolMap =>
        CardPlayRound.Current?.ChangeEventsPoolMap ?? new();

    public static Faction LastChangeEventByFaction =>
        CardPlayRound.Current?.LastChangeEventByFaction ?? Faction.GERMANY;

    public static FactionTeam LastChangeEventByTeam =>
        CardPlayRound.Current?.LastChangeEventByTeam ?? FactionTeam.AXIS;

    public static ChangeEvent LastNoneNewCardChangeEvent =>
        CardPlayRound.Current?.LastNoneNewCardChangeEvent;

    public static List<Faction> RequestOrder =>
        CardPlayRound.Current?.RequestOrder ?? CardPlayRound.AxisFirstOrder;

    public static List<Faction> AxisFirstOrder => CardPlayRound.AxisFirstOrder;
    public static List<Faction> AlliesFirstOrder => CardPlayRound.AlliesFirstOrder;

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    public static void ClearPool() => CardPlayRound.Current?.ClearPool();

    // ── Query pass-through ─────────────────────────────────────────────────────
    public static List<T> GetChangeEvents<T>() where T : ChangeEvent =>
        CardPlayRound.Current?.GetChangeEvents<T>() ?? new();

    // ── Pipeline pass-through ──────────────────────────────────────────────────

    /// <summary>Execute a card by card ID (delegates to the active CardPlayRound).</summary>
    public static Task DoCard(int cardId) =>
        CardPlayRound.Current?.DoCard(cardId) ?? Task.CompletedTask;

    /// <summary>
    /// Process a change event through the full pipeline.
    /// When no round is active (e.g. draw/discard steps with IsTrigger=false), applies the event directly.
    /// </summary>
    public static Task DoChangeEvent(ChangeEvent changeEvent)
    {
        if (CardPlayRound.Current != null)
            return CardPlayRound.Current.DoChangeEvent(changeEvent);

        // No active round — apply directly without reaction chain
        return changeEvent.ApplyChange().ContinueWith(_ => { });
    }

    public static Task<int> RequestCardPlay(Faction faction) =>
        CardPlayRound.Current?.RequestCardPlay(faction) ?? Task.FromResult(-1);

    public static Task<int> RequestPlay(Faction faction) =>
        CardPlayRound.Current?.RequestPlay(faction) ?? Task.FromResult(-1);

    public static Task<int> RequestBlock(Faction faction) =>
        CardPlayRound.Current?.RequestBlock(faction) ?? Task.FromResult(-1);

    public static List<int> GetNextActions(Faction faction) =>
        CardPlayRound.Current?.GetNextActions(faction) ?? new();

    public static List<int> GetAfterReactionOptions(Faction faction) =>
        CardPlayRound.Current?.GetAfterReactionOptions(faction) ?? new();

    public static Task<List<int>> BlockChangeEvents(Faction faction) =>
        CardPlayRound.Current?.GetBlockOptions(faction) ?? Task.FromResult(new List<int>());

    public static List<int> PlayableCardIds(Faction faction) =>
        CardPlayRound.Current?.PlayableCardIds(faction) ?? new();
}
