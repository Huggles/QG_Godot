using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Builds the in-game music playlist and hands it to <see cref="AudioManager"/>.
///
/// The rule is: shuffle every in-game track, then put a track belonging to a faction the *local*
/// player controls on top, so a game opens on your own theme and then drifts through the rest of the
/// soundtrack. The playlist repeats in that same order for the whole session.
///
/// The faction → folder mapping is the enum name lowercased (<c>UNITED_KINGDOM</c> →
/// <c>music/united_kingdom</c>), the same convention <c>FactionData.factionName</c> uses for
/// textures. The music root itself is never included, which is what keeps the menu theme out of the
/// game playlist.
///
/// This lives here rather than in <see cref="AudioManager"/> because the audio service must not know
/// what a faction is — it only knows folders.
/// </summary>
public static class GameMusic
{
    /// <summary>Folder under the music root holding general in-game tracks, belonging to no faction.</summary>
    private const string GeneralFolder = "game";

    /// <summary>
    /// Starts the playlist for the player at this peer. Safe to call on every peer: the opening track
    /// is chosen from <see cref="PlayerFactionRegistry.GetLocalPlayerFactions"/>, so each machine gets
    /// its own. A no-op headless, where <see cref="AudioManager"/> is disabled outright.
    /// </summary>
    public static void StartForLocalPlayer()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null)
        {
            return;
        }

        List<string> playlist = StaticGameData.PlayableFactions
            .Select(FolderFor)
            .Prepend(GeneralFolder)
            .SelectMany(audio.TracksInFolder)
            .ToList();

        if (playlist.Count == 0)
        {
            DebugUtilities.PrintPeerFinest("GameMusic: no in-game music found, staying silent.");
            return;
        }

        Shuffle(playlist);
        PromoteLocalFactionTrack(audio, playlist);

        DebugUtilities.PrintPeerFinest($"GameMusic: playlist ({playlist.Count}): {string.Join(", ", playlist)}");
        AudioManager.PlayPlaylist(playlist);
    }

    private static string FolderFor(Faction faction) => faction.ToString().ToLower();

    /// <summary>
    /// Moves a track from one of the local player's factions to the front of the playlist, so that is
    /// what the game opens on. Leaves the order alone when the local player controls no faction (a
    /// dedicated server, or a spectator) or when none of their factions has any music.
    ///
    /// In single player the local player controls every faction, so this is a random faction's theme
    /// — which is the intent there.
    /// </summary>
    private static void PromoteLocalFactionTrack(AudioManager audio, List<string> playlist)
    {
        List<string> ownTracks = PlayerFactionRegistry.GetLocalPlayerFactions()
            .Select(faction => audio.TracksInFolder(FolderFor(faction)))
            .Where(tracks => tracks.Count > 0)
            // Picked per faction rather than over the pooled tracks so every faction the player holds
            // is equally likely to open the game, however many tracks each of them has.
            .Select(tracks => tracks[GD.RandRange(0, tracks.Count - 1)])
            .ToList();

        if (ownTracks.Count == 0)
        {
            DebugUtilities.PrintPeerFinest("GameMusic: local player has no faction music; playing the shuffled list as-is.");
            return;
        }

        string opening = ownTracks[GD.RandRange(0, ownTracks.Count - 1)];
        playlist.Remove(opening);
        playlist.Insert(0, opening);
    }

    /// <summary>
    /// Fisher-Yates over Godot's global RNG, deliberately *not* <see cref="GameRandom"/>: that stream
    /// is seeded from the host and shared by every peer to keep gameplay reproducible, so drawing
    /// music picks from it would both desync the peers and change what a replayed seed does.
    /// </summary>
    private static void Shuffle(List<string> tracks)
    {
        for (int i = tracks.Count - 1; i > 0; i--)
        {
            int j = GD.RandRange(0, i);
            (tracks[i], tracks[j]) = (tracks[j], tracks[i]);
        }
    }
}
