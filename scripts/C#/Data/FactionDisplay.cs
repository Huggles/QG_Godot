using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The single source of user-facing faction text. Everything a player reads — history rows, action
/// banners, modal titles, "waiting on…" — goes through here, so "United Kingdom (Bob)" is spelled one
/// way in one place instead of the four inconsistent ways it used to be (FactionData.Label,
/// FactionState.FactionLabel, DeckState.FactionLabel, and a bare Faction.ToString() that put
/// UNITED_KINGDOM in front of players).
///
/// NOT for card rules/guidance prose ("Battle a Soviet Army adjacent to a German Army"): that is text
/// about the rules, not about who is sitting at the table, and it wants the adjectival form
/// (<see cref="FactionData.FactionAdjactiveLabel"/>). Also not for the CLI views, which deliberately
/// print the raw enum because that is what .qgc scripts type.
/// </summary>
public static class FactionDisplay
{
    /// <summary>
    /// Pretty faction name only: "United Kingdom", never "UNITED_KINGDOM".
    ///
    /// Reads <see cref="StaticGameData.FactionDataMap"/> rather than FactionState.ForEnum because this
    /// has to work wherever text is drawn — a menu, a headless CLI run, a unit test — and ForEnum
    /// dereferences MultiplayerSession.Instance (null outside a game) and returns null for a faction
    /// that is not in the current state. The enum-name fallback covers Faction.NONE and Faction.ALL,
    /// which have no row in QGData_Factions_V2.json at all, and the window before that JSON is loaded.
    /// </summary>
    public static string Label(this Faction faction)
    {
        FactionData data = StaticGameData.FactionDataMap.GetValueOrDefault(faction);
        return string.IsNullOrEmpty(data?.Label) ? PrettyEnumName(faction) : data.Label;
    }

    /// <summary>
    /// "United Kingdom (Bob)", or plain "United Kingdom" when there is no meaningful name behind the
    /// faction — see <see cref="PlayerNameFor"/>.
    ///
    /// Must never throw: CardPlayRound puts SummaryText() on the wire as
    /// <c>InputRequest.TriggerSummaryText</c>, so a lookup that threw here would take down a turn
    /// rather than just garble a label. Hence GetValueOrDefault/TryGetValue throughout, and never
    /// FactionData.FactionAdjactiveLabel, which indexes a dictionary missing NONE and ALL.
    /// </summary>
    public static string WithPlayer(this Faction faction)
    {
        string player = PlayerNameFor(faction);
        return player == null ? faction.Label() : $"{faction.Label()} ({player})";
    }

    /// <summary>
    /// The name that goes in the parentheses, or null when there should be none. The whole suppression
    /// policy lives here, in one place: one peer owning everything is the local player themselves, and
    /// "(you)" six times over is noise — which is exactly the shape single player, debug solo and the
    /// CLI boot into.
    /// </summary>
    public static string PlayerNameFor(Faction faction)
    {
        // Above the single-player suppression, and both orderings are load-bearing:
        //
        //  - Above IsSinglePlayer, because a solo game against bots is exactly where naming them
        //    matters most, and that guard would otherwise return null and hide it.
        //  - At all, because an AI seat is registered under a synthetic id owned by the host — so
        //    GetDisplayNameForFaction would answer with the HOST'S name, labelling every bot in a
        //    multiplayer game "Germany (Alice)". This class is the single source of user-facing
        //    faction text, so this one line covers history rows, action banners, modal titles and the
        //    "Waiting on…" label at once.
        if (PlayerFactionRegistry.IsFactionAi(faction)) return "AI";

        if (PlayerFactionRegistry.IsSinglePlayer) return null;
        return PlayerFactionRegistry.GetDisplayNameForFaction(faction);
    }

    /// <summary>UNITED_STATES → "United States". Only used when there is no FactionData row.</summary>
    private static string PrettyEnumName(Faction faction)
        => string.Join(" ", faction.ToString()
            .Split('_')
            .Select(word => word.Length == 0 ? word : char.ToUpper(word[0]) + word.Substring(1).ToLower()));
}
