using Godot;
using System.Collections.Generic;

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
	/// Release the Steam networking sessions a peer was holding, before it is closed.
	///
	/// <c>MultiplayerPeer.Close()</c> tears down Godot's view of the connections; the Steam session
	/// underneath each one lives on, and a later session between the same two users inherits its
	/// state — including the peer identity the previous connection was using. That produced a re-host
	/// in which the host addressed a client by the old session's id while the client's own peer had
	/// generated a new one, so nothing in the lobby read as belonging to that client.
	///
	/// Call with the peer still open: the id → Steam id lookup is the peer's own map. A no-op for
	/// anything that is not a Steam peer, which is every ENet session.
	/// </summary>
	public static void ReleaseSession(MultiplayerPeer peer, IEnumerable<int> peerIds)
	{
		if (peer == null || !IsSupported || SteamworksApi.Instance == null) return;

		// By class rather than by `is`: the wrapper is a script attached to a GDExtension object, and
		// the instance Godot hands back from MultiplayerPeer is not necessarily the managed wrapper the
		// factory built. This is the same check Bind performs before it will attach.
		if (!ClassDB.IsParentClass("SteamMultiplayerPeer", peer.GetClass())) return;

		SteamMultiplayerPeer steamPeer = SteamMultiplayerPeer.Bind(peer);
		if (steamPeer == null) return;

		foreach (int peerId in peerIds)
		{
			// Guarded individually: one peer whose id no longer resolves must not stop the rest being
			// released, and leaving even one session open reintroduces the bug this exists to prevent.
			Guard.Try(() =>
			{
				long steamId = steamPeer.GetSteamIdForPeerId(peerId);
				SteamworksApi.Instance.CloseNetworkingSessionWith(steamId);
			}, $"SteamPeerFactory.ReleaseSession:{peerId}");
		}
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
