using Godot;
using System.Threading.Tasks;

/// <summary>
/// The Steam side of starting a hosted game, shared by the two screens that can start one: the main
/// menu's Host flow and the load-game screen's restore flow.
///
/// Extracted so there is one lobby-creation path rather than two copies that drift. The rule it
/// encodes is the reason it has to run before navigating at all: the Steam lobby must exist before the
/// multiplayer peer tries to host on it, and a failure has to leave the player on the screen they
/// started from — with the Godot option still one click away — rather than stranding them on a lobby
/// that never finished hosting.
/// </summary>
public static class HostLaunch
{
    /// <summary>
    /// Create the Steam lobby and stamp its metadata. Returns the lobby id, or 0 if it could not be
    /// created — in which case the player has already been told why, and the caller should simply stop.
    /// </summary>
    /// <param name="scenarioTitle">
    /// Published immediately so a friend's list describes the game correctly from its first refresh,
    /// rather than showing the wrong scenario until something else happens to update it.
    /// </param>
    public static async Task<long> CreateSteamLobbyAsync(Node parent, HostOptionsDialog.Result choice,
                                                         string scenarioTitle)
    {
        MenuNotice busy = MenuNotice.ShowBusy(parent, "Creating Steam lobby...");

        long lobbyId = await SteamworksApi.Instance.CreateLobbyAsync(choice.Privacy, choice.MaxPlayers);

        if (!GodotObject.IsInstanceValid(parent)) return 0;
        busy.Dismiss();

        if (lobbyId == 0)
        {
            await MenuNotice.ShowMessageAsync(parent, "Could not create a Steam lobby",
                "Steam did not create the lobby. You can still host over Godot networking.");
            return 0;
        }

        SteamworksApi.Instance.PublishLobbyMetadata(scenarioTitle ?? string.Empty, choice.MaxPlayers);
        return lobbyId;
    }
}
