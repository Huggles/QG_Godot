using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Settings fixed for one whole session and identical on every peer: which expansions this game is played
/// with.
///
/// Set by <see cref="MultiplayerSession.StartSession"/> on every peer, from the list the host resolved,
/// before the game mode builds any state. Unlike GameFlow.OpeningDiscardEnabled, which only the host
/// reads, this cannot be host-only: an expansion changes what every peer builds for itself (units, decks,
/// the step table), so a peer that disagreed would desync from the first event.
/// </summary>
public static class GameOptions
{
    /// <summary>This session's expansions, in <see cref="ExpansionIds"/> order. Empty for the base game.</summary>
    public static IReadOnlyList<string> EnabledExpansions { get; private set; } = Array.Empty<string>();

    public static bool IsExpansionEnabled(string id) => EnabledExpansions.Contains(id);

    /// <summary>What those expansions add to the rules, e.g. extra turn steps. Empty for the base game.</summary>
    public static RuleSet Rules { get; private set; } = new();

    /// <summary>
    /// Fixes this session's expansions. Throws, naming what is missing, when this install cannot play one
    /// of them. The menus refuse to start such a game, so reaching this means a peer the lobby could not
    /// vet (a CLI run, a rejoin after an uninstall); stopping here beats a desync several turns in.
    /// </summary>
    public static void SetForSession(IEnumerable<string> expansionIds)
    {
        List<string> ids = ExpansionIds.Normalize(expansionIds);
        List<string> missing = ExpansionRegistry.Unavailable(ids);
        if (missing.Count > 0)
            throw new InvalidOperationException($"This game is played with {ExpansionIds.Describe(ids)}, which cannot be played here: {string.Join("; ", missing)}");

        EnabledExpansions = ids;
        Rules = RuleSet.For(ids);
        DebugUtilities.PrintPeer($"Session expansions: {ExpansionIds.Describe(ids)}");
    }
}
