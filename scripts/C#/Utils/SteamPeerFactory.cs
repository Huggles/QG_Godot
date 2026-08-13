using Godot;

using GDExtension.Wrappers;

/// <summary>
/// Builds the Steam transport. This is the only file in the game that names
/// <see cref="SteamMultiplayerPeer"/> — everything below the menu layer (NetworkApi,
/// MultiplayerSession, GameFlow) only ever touches <c>Multiplayer.MultiplayerPeer</c> and RPCs, so
/// the rest of the codebase stays transport-agnostic and the ENet path is completely unaffected.
///
/// The wrapper in <c>GDExtensionWrappers/</c> is generated from the installed GodotSteam build by
/// the C# wrapper generator addon; regenerate it after a GodotSteam upgrade.
/// </summary>
public static class SteamPeerFactory
{
	/// <summary>
	/// False when the plain GodotSteam build is installed instead of the MultiplayerPeer flavour.
	/// Orthogonal to <see cref="SteamworksApi.IsAvailable"/>: Steam can be running perfectly while
	/// this is still false, so both have to be checked before offering a Steam option.
	/// </summary>
	public static bool IsSupported => ClassDB.ClassExists("SteamMultiplayerPeer");

	/// <summary>
	/// Hosts on an existing Steam lobby. The lobby must already be created and owned by this client
	/// — the peer asserts ownership internally — so <see cref="SteamworksApi.CreateLobbyAsync"/> has
	/// to have completed first.
	/// </summary>
	public static MultiplayerPeer CreateHost(long lobbyId, out string error)
	{
		if (!TryPrepare(lobbyId, out SteamMultiplayerPeer peer, out error)) return null;

		Error result = peer.HostWithLobby(lobbyId);
		if (result != Error.Ok)
		{
			peer.Free();
			error = $"Steam refused to host the lobby ({result}).";
			return null;
		}

		error = null;
		return peer;
	}

	/// <summary>
	/// Connects to a lobby this client has already joined. The peer refuses to connect to a lobby it
	/// is not a member of, so <see cref="SteamworksApi.JoinLobbyAsync"/> has to have completed first.
	/// </summary>
	public static MultiplayerPeer CreateClient(long lobbyId, out string error)
	{
		if (!TryPrepare(lobbyId, out SteamMultiplayerPeer peer, out error)) return null;

		Error result = peer.ConnectToLobby(lobbyId);
		if (result != Error.Ok)
		{
			peer.Free();
			error = $"Steam refused the connection ({result}).";
			return null;
		}

		error = null;
		return peer;
	}

	/// <summary>
	/// Shared guard rail: both entry points depend on lobby state that is set up elsewhere, and a
	/// mismatch otherwise surfaces as a bare ERR_CANT_CREATE with no clue about the cause.
	/// </summary>
	private static bool TryPrepare(long lobbyId, out SteamMultiplayerPeer peer, out string error)
	{
		peer = null;

		if (!IsSupported)
		{
			error = "this build of GodotSteam has no Steam networking support.";
			return false;
		}
		if (lobbyId == 0)
		{
			error = "no Steam lobby was created.";
			return false;
		}
		if (SteamworksApi.Instance?.CurrentLobbyId != lobbyId)
		{
			error = "the Steam lobby was lost.";
			return false;
		}

		peer = SteamMultiplayerPeer.Instantiate();
		if (peer == null)
		{
			error = "could not create the Steam peer.";
			return false;
		}

		if (GameSettings.IsDebugMultiplayer)
			peer.DebugLevel = SteamMultiplayerPeer.DebugLevelEnum.Peer;

		error = null;
		return true;
	}
}
