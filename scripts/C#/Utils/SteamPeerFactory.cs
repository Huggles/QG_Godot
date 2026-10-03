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
	/// The one native peer this process ever creates; every session reuses it.
	///
	/// The GodotSteam peer subscribes to Steam's connection-status callbacks, and those are process
	/// wide: every live peer object sees every connection, not just its own. A peer from a finished
	/// session is not destroyed when it is closed and unassigned — a C# wrapper still references it
	/// until the GC gets round to it — so it keeps reacting. On a rejoin it adopted the new connection
	/// to the host and, once that connected, sent its own stale peer id down it. The host takes the
	/// first id it hears, so it addressed the client by the previous session's id while the client's
	/// live peer had generated a new one (see SessionIdentity). Closing alone cannot fix that, since the
	/// callbacks only unregister when the native object is destroyed; reusing the instance means there
	/// is never a second one to answer.
	///
	/// Reuse is supported by the peer itself: Close() returns it to CONNECTION_DISCONNECTED and the
	/// next HostWithLobby / ConnectToLobby sets every field a session depends on, including a freshly
	/// generated unique id for a client.
	/// </summary>
	private static SteamMultiplayerPeer _peer;

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
			peer.Close();
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
			peer.Close();
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

		if (_peer == null || !GodotObject.IsInstanceValid(_peer))
			_peer = SteamMultiplayerPeer.Instantiate();
		if (_peer == null)
		{
			error = "could not create the Steam peer.";
			return false;
		}

		// Not every exit path closes it — the connect timeout on the friends screen only unassigns it —
		// and the peer refuses to start a session while it still thinks it is in one. Idempotent.
		_peer.Close();
		peer = _peer;

		if (GameSettings.IsDebugMultiplayer)
			peer.DebugLevel = SteamMultiplayerPeer.DebugLevelEnum.Peer;

		error = null;
		return true;
	}
}
