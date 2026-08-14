using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Games.Indiegesindel;

/// <summary>Lobby visibility, exposed so no menu code has to reference <see cref="Steam.LobbyType"/>.</summary>
public enum SteamLobbyPrivacy
{
	/// <summary>Invite-only. Deliberately does NOT appear in friends' game lists.</summary>
	Private     = 0,
	/// <summary>Visible to friends, who can join straight from the friends-games list.</summary>
	FriendsOnly = 1,
}

/// <summary>One friend-hosted lobby, as shown on <c>SteamFriendLobbiesScreen</c>.</summary>
public sealed record FriendLobby(
	long   LobbyId,
	ulong  HostSteamId,
	string HostName,
	string ScenarioTitle,
	int    Members,
	int    MaxMembers,
	string DiscoveredVia)
{
	public bool IsFull => MaxMembers > 0 && Members >= MaxMembers;
}

/// <summary>Friend presence, exposed so no menu code has to reference <see cref="Steam.PersonaState"/>.</summary>
public enum SteamPresence
{
	Online,
	LookingToPlay,
	Away,
	Busy,
	Snooze,
}

/// <summary>
/// One online friend, as shown in <c>InviteFriendsDialog</c>.
///
/// <see cref="InThisGame"/> is the closest thing Steam offers to "owns this game": the client API has no
/// ownership query at all, only what a user is running right now.
/// </summary>
public sealed record SteamFriend(
	ulong         SteamId,
	string        Name,
	SteamPresence Presence,
	bool          InThisGame,
	bool          AlreadyInLobby);

/// <summary>
/// The only place in the game that talks to Steamworks. Everything else asks
/// <see cref="IsAvailable"/> and calls the async helpers here, so a missing or signed-out Steam
/// can never break the ENet path or the menu.
///
/// Autoloaded (see <c>project.godot</c>). GodotSteam initialises itself at engine start via the
/// <c>[steam]</c> project settings, so this class does not call SteamInit — it only verifies that
/// init actually worked. It DOES pump callbacks: GodotSteam logs
/// "Cannot use auto-initialization and embed callbacks together currently. Embed callbacks ignored;
/// call run_callbacks() manually" at startup, so with auto-init on, <c>embed_callbacks</c> is a
/// no-op and nothing else drives the callback queue. Without the pump below, every Steam signal
/// (lobby created, lobby joined, lobby data) would simply never arrive and each async call here
/// would sit until its timeout.
/// </summary>
public partial class SteamworksApi : SingletonNode<SteamworksApi>
{
	/// <summary>Lobby metadata keys. One place, so the writer and the reader cannot drift apart.</summary>
	public static class LobbyKeys
	{
		public const string Game        = "qg_game";
		public const string Version     = "qg_version";
		public const string HostName    = "host_name";
		public const string HostSteamId = "host_steam_id";
		public const string Scenario    = "scenario";
		public const string MaxPlayers  = "max_players";
	}

	/// <summary>
	/// Marks a lobby as ours. Not optional: app 480 (Spacewar) is shared by every GodotSteam
	/// developer, so without this filter the friends list can surface strangers' test lobbies.
	/// </summary>
	public const string GameSentinel  = "quartermaster_general";
	/// <summary>Bumped when the lobby handshake changes, so mismatched builds hide each other.</summary>
	public const string LobbyProtocol = "1";

	private const double OpTimeoutSeconds        = 15.0;
	private const double LobbyDataTimeoutSeconds = 5.0;

	private Steam  _steam;
	private bool   _available;
	private string _reason = "Steam integration not initialised";

	public static bool   IsAvailable       => Instance is { _available: true };
	public static string UnavailableReason => Instance?._reason ?? "Steam integration not loaded";

	public ulong  LocalSteamId     { get; private set; }
	public string LocalPersonaName { get; private set; } = "Player";
	public long   AppId            { get; private set; }

	/// <summary>0 when not in a lobby.</summary>
	public long CurrentLobbyId { get; private set; }
	public bool IsLobbyOwner   { get; private set; }

	/// <summary>Steam overlay "Join game" or an accepted invite. Carries the lobby to join.</summary>
	public event Action<long> JoinRequested;
	/// <summary>A member joined or left <see cref="CurrentLobbyId"/>.</summary>
	public event Action LobbyMembershipChanged;
	/// <summary>Steam finished downloading a user's avatar. Carries the user it belongs to.</summary>
	public event Action<ulong> AvatarUpdated;

	// ══════════════════════════════════════════════════════════════════════════
	// Lifecycle
	// ══════════════════════════════════════════════════════════════════════════

	public override void _Ready()
	{
		base._Ready();                      // sets Instance
		Guard.Try(InitInternal, "SteamworksApi._Ready");
	}

	private void InitInternal()
	{
		// Off until init proves out, so an unavailable Steam costs nothing per frame and every
		// early return below is automatically safe.
		SetProcess(false);

		// A dedicated server or CLI run has no signed-in user and will only ever use ENet.
		// Bailing here also means none of the Steam signal handlers are ever wired up.
		if (GameContext.IsHeadless)
		{
			_reason = "headless process";
			return;
		}

		// Cached exactly once for the process lifetime: GetSingleton() calls Bind(), which attaches
		// the wrapper script to the engine singleton, and the C# event backing fields live on the
		// managed instance. Re-fetching per call would drop subscriptions.
		_steam = Steam.GetSingleton();
		if (_steam == null)
		{
			_reason = "GodotSteam extension not loaded";
			return;
		}

		if (!_steam.IsSteamRunning())
		{
			_reason = "Steam client is not running";
			DebugUtilities.PrintPeer("Steam unavailable: client not running");
			return;
		}

		// IsSteamRunning only reports the client process. It is still possible for SteamAPI_Init to
		// have failed (wrong app id, no steam_appid.txt), which leaves every interface null and makes
		// the first real call print "Friends class not found". A non-zero Steam ID proves init worked.
		ulong steamId = _steam.GetSteamId();
		if (steamId == 0)
		{
			_reason = "Steam API failed to initialise (check the app id)";
			DebugUtilities.PrintPeerError("Steam unavailable: SteamAPI_Init did not complete");
			return;
		}

		_available       = true;
		LocalSteamId     = steamId;
		LocalPersonaName = _steam.GetPersonaName();
		AppId            = _steam.GetAppId();
		SetProcess(true);

		// Note the parameter order: lobby_created is (result, lobby_id), NOT (lobby_id, result).
		_steam.LobbyCreatedSignal    += OnLobbyCreated;
		_steam.LobbyJoinedSignal     += OnLobbyJoined;
		_steam.LobbyDataUpdateSignal += OnLobbyDataUpdate;
		_steam.LobbyChatUpdateSignal += OnLobbyChatUpdate;
		_steam.JoinRequestedSignal   += OnJoinRequested;
		_steam.LobbyInviteSignal     += OnLobbyInvite;
		_steam.AvatarLoadedSignal    += OnAvatarLoaded;

		// Peer support is logged separately because it fails independently of Steam itself: the plain
		// GodotSteam build initialises fine but ships no SteamMultiplayerPeer, and an export built
		// against it would otherwise only reveal that on the first host attempt.
		DebugUtilities.PrintPeer(
			$"Steam ready: {LocalPersonaName} ({LocalSteamId}), app {AppId}, "
			+ $"networking: {(SteamPeerFactory.IsSupported ? "available" : "MISSING")}");
	}

	/// <summary>
	/// Drives the Steam callback queue. Guarded on <see cref="_available"/>, which is only ever true
	/// when <see cref="_steam"/> is non-null and init succeeded — the unguarded version of this was
	/// throwing a NullReferenceException every frame whenever Steam was not running.
	/// </summary>
	public override void _Process(double delta)
	{
		if (!_available) return;
		_steam.RunCallbacks();
	}

	public override void _Notification(int what)
	{
		// Releasing the lobby on the way out stops a stale entry lingering in friends' lists.
		if (what == NotificationWMCloseRequest || what == NotificationPredelete)
			Guard.Try(LeaveCurrentLobby, "SteamworksApi.Shutdown");
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Lobby lifecycle
	// ══════════════════════════════════════════════════════════════════════════

	private TaskCompletionSource<long> _pendingCreate;
	private TaskCompletionSource<long> _pendingJoin;
	private readonly Dictionary<long, TaskCompletionSource<bool>> _pendingLobbyData = new();

	/// <summary>
	/// Creates a lobby and waits until this client is both its owner and a joined member — the exact
	/// precondition <c>SteamMultiplayerPeer.HostWithLobby</c> asserts on.
	/// Returns the lobby id, or 0 on failure.
	/// </summary>
	public async Task<long> CreateLobbyAsync(SteamLobbyPrivacy privacy, int maxMembers)
	{
		if (!_available)           return 0;
		if (_pendingCreate != null) return 0;   // one create in flight at a time

		maxMembers = Math.Clamp(maxMembers, 2, 6);

		// Steam auto-joins the creator, so the join callback is armed before the create call.
		_pendingCreate = NewSource<long>();
		_pendingJoin   = NewSource<long>();

		_steam.CreateLobby((long)privacy, maxMembers);

		long lobbyId = await WithTimeout(_pendingCreate, 0L, OpTimeoutSeconds);
		_pendingCreate = null;
		if (lobbyId == 0)
		{
			_pendingJoin = null;
			DebugUtilities.PrintPeerError("Steam: lobby creation failed or timed out");
			return 0;
		}

		long response = await WithTimeout(_pendingJoin, 0L, OpTimeoutSeconds);
		_pendingJoin = null;
		if (response != (long)Steam.ChatRoomEnterResponse.Success)
		{
			DebugUtilities.PrintPeerError($"Steam: created lobby {lobbyId} but could not enter it ({response})");
			_steam.LeaveLobby(lobbyId);
			return 0;
		}

		CurrentLobbyId = lobbyId;
		IsLobbyOwner   = true;
		_steam.SetLobbyJoinable(lobbyId, true);
		DebugUtilities.PrintPeer($"Steam: hosting lobby {lobbyId} ({privacy}, max {maxMembers})");
		return lobbyId;
	}

	/// <summary>Writes the metadata the friends list reads. Owner only; a safe no-op otherwise.</summary>
	public void PublishLobbyMetadata(string scenarioTitle, int maxPlayers)
	{
		if (!_available || CurrentLobbyId == 0 || !IsLobbyOwner) return;

		_steam.SetLobbyData(CurrentLobbyId, LobbyKeys.Game,        GameSentinel);
		_steam.SetLobbyData(CurrentLobbyId, LobbyKeys.Version,     LobbyProtocol);
		_steam.SetLobbyData(CurrentLobbyId, LobbyKeys.HostName,    LocalPersonaName);
		_steam.SetLobbyData(CurrentLobbyId, LobbyKeys.HostSteamId, LocalSteamId.ToString());
		_steam.SetLobbyData(CurrentLobbyId, LobbyKeys.Scenario,    scenarioTitle ?? string.Empty);
		_steam.SetLobbyData(CurrentLobbyId, LobbyKeys.MaxPlayers,  maxPlayers.ToString());
	}

	/// <summary>
	/// Joins <paramref name="lobbyId"/>. Must complete before <c>ConnectToLobby</c> — the peer
	/// refuses to connect to a lobby this client is not already a member of.
	/// </summary>
	public async Task<(bool Ok, string Error)> JoinLobbyAsync(long lobbyId)
	{
		if (!_available)          return (false, UnavailableReason);
		if (_pendingJoin != null) return (false, "Already joining a lobby");

		_pendingJoin = NewSource<long>();
		_steam.JoinLobby(lobbyId);

		long response = await WithTimeout(_pendingJoin, 0L, OpTimeoutSeconds);
		_pendingJoin = null;

		if (response == (long)Steam.ChatRoomEnterResponse.Success)
		{
			CurrentLobbyId = lobbyId;
			IsLobbyOwner   = (ulong)_steam.GetLobbyOwner(lobbyId) == LocalSteamId;
			return (true, null);
		}

		return (false, DescribeEnterResponse(response));
	}

	public void LeaveCurrentLobby()
	{
		if (!_available || CurrentLobbyId == 0) return;

		DebugUtilities.PrintPeer($"Steam: leaving lobby {CurrentLobbyId}");
		_steam.LeaveLobby(CurrentLobbyId);
		CurrentLobbyId = 0;
		IsLobbyOwner   = false;
	}

	/// <summary>
	/// Sends a lobby invite straight to a friend's Steam client, bypassing the overlay entirely — the
	/// overlay does not render in the editor, which made the invite path untestable during development.
	///
	/// The invitee needs no new code: the invite arrives on their <c>LobbyInviteSignal</c>, which
	/// <see cref="OnLobbyInvite"/> already turns into a <see cref="JoinRequested"/>. A friend who is online
	/// but not running the game gets it as a Steam notification that launches the game.
	///
	/// Returns false when the invite could not be sent. Steam reports no callback for this, so a false
	/// return is the only failure signal there is.
	/// </summary>
	public bool InviteToCurrentLobby(ulong friendSteamId)
	{
		if (!_available || CurrentLobbyId == 0 || friendSteamId == 0) return false;
		return _steam.InviteUserToLobby(CurrentLobbyId, (long)friendSteamId);
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Friend lobby discovery
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Enumerates lobbies our friends are currently in.
	///
	/// RequestLobbyList is deliberately NOT used: it only returns public lobbies, so friends-only
	/// and private ones — the two kinds this game creates — would never show up. Walking the friends
	/// list and reading each friend's current lobby is the only way to see them.
	/// </summary>
	public async Task<List<FriendLobby>> GetFriendLobbiesAsync()
	{
		var result = new List<FriendLobby>();
		if (!_available) return result;

		// FlagImmediate = actual friends. The default (FlagAll) would also include blocked users
		// and pending requests, which must never be surfaced as joinable games.
		const long flags = (long)Steam.FriendFlags.FlagImmediate;

		var candidates = new Dictionary<long, string>();   // lobbyId -> name of the friend we saw it through
		long friendCount = _steam.GetFriendCount(flags);
		for (long i = 0; i < friendCount; i++)
		{
			long friendId = _steam.GetFriendByIndex(i, flags);
			Godot.Collections.Dictionary played = _steam.GetFriendGamePlayed(friendId);
			if (played == null || played.Count == 0) continue;

			if (!played.TryGetValue("id", out Variant playedApp) || playedApp.As<long>() != AppId) continue;
			if (!played.TryGetValue("lobby", out Variant playedLobby)) continue;

			long lobbyId = playedLobby.As<long>();
			if (lobbyId == 0 || candidates.ContainsKey(lobbyId)) continue;

			candidates[lobbyId] = _steam.GetFriendPersonaName(friendId);
		}

		// Metadata for a lobby we are not a member of has to be pulled explicitly; the answer comes
		// back on LobbyDataUpdateSignal. Requests run concurrently so one slow lobby cannot stall
		// the whole refresh.
		var waits = new List<Task<bool>>();
		foreach (long lobbyId in candidates.Keys)
		{
			var source = NewSource<bool>();
			_pendingLobbyData[lobbyId] = source;

			if (_steam.RequestLobbyData(lobbyId))
				waits.Add(WithTimeout(source, false, LobbyDataTimeoutSeconds));
			else
				_pendingLobbyData.Remove(lobbyId);
		}
		if (waits.Count > 0) await Task.WhenAll(waits);
		_pendingLobbyData.Clear();

		foreach ((long lobbyId, string via) in candidates)
		{
			if (_steam.GetLobbyData(lobbyId, LobbyKeys.Game)    != GameSentinel)  continue;
			if (_steam.GetLobbyData(lobbyId, LobbyKeys.Version) != LobbyProtocol) continue;

			string hostName = _steam.GetLobbyData(lobbyId, LobbyKeys.HostName);
			result.Add(new FriendLobby(
				LobbyId:       lobbyId,
				HostSteamId:   ulong.TryParse(_steam.GetLobbyData(lobbyId, LobbyKeys.HostSteamId), out ulong host) ? host : 0,
				HostName:      string.IsNullOrWhiteSpace(hostName) ? via : hostName,
				ScenarioTitle: _steam.GetLobbyData(lobbyId, LobbyKeys.Scenario),
				Members:       (int)_steam.GetNumLobbyMembers(lobbyId),
				MaxMembers:    (int)_steam.GetLobbyMemberLimit(lobbyId),
				DiscoveredVia: via));
		}

		return result;
	}

	/// <summary>Persona names of everyone currently in <paramref name="lobbyId"/>.</summary>
	public List<string> GetLobbyMemberNames(long lobbyId)
	{
		var names = new List<string>();
		if (!_available || lobbyId == 0) return names;

		long count = _steam.GetNumLobbyMembers(lobbyId);
		for (long i = 0; i < count; i++)
			names.Add(_steam.GetFriendPersonaName(_steam.GetLobbyMemberByIndex(lobbyId, i)));

		return names;
	}

	/// <summary>Persona name for any Steam user Steam has cached. Falls back to the raw id.</summary>
	public string PersonaNameFor(ulong steamId)
	{
		if (!_available || steamId == 0) return "Player";
		string name = _steam.GetFriendPersonaName((long)steamId);
		return string.IsNullOrWhiteSpace(name) ? steamId.ToString() : name;
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Friends (invite list)
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Every friend who is not offline, with the ones currently running this game first.
	///
	/// Synchronous on purpose: unlike <see cref="GetFriendLobbiesAsync"/>, which has to wait on
	/// LobbyDataUpdateSignal for lobbies we are not a member of, every call below answers from the local
	/// friends cache immediately.
	///
	/// The list is deliberately NOT filtered down to people who own the game: the client API has no
	/// ownership query, and this build runs on app 480 (Spacewar) which every Steam user can launch. So
	/// ownership is surfaced as the <see cref="SteamFriend.InThisGame"/> hint and the sort order instead,
	/// which is exactly what Steam's own invite dialog does.
	/// </summary>
	public List<SteamFriend> GetOnlineFriends()
	{
		var friends = new List<SteamFriend>();
		if (!_available) return friends;

		// Anyone already here is shown as such rather than being invitable again.
		var members = new HashSet<ulong>();
		if (CurrentLobbyId != 0)
		{
			long memberCount = _steam.GetNumLobbyMembers(CurrentLobbyId);
			for (long i = 0; i < memberCount; i++)
				members.Add((ulong)_steam.GetLobbyMemberByIndex(CurrentLobbyId, i));
		}

		// FlagImmediate = actual friends. The default (FlagAll) would also include blocked users and
		// pending requests, who must never be invitable.
		const long flags = (long)Steam.FriendFlags.FlagImmediate;

		long friendCount = _steam.GetFriendCount(flags);
		for (long i = 0; i < friendCount; i++)
		{
			long friendId = _steam.GetFriendByIndex(i, flags);
			if (friendId == 0 || (ulong)friendId == LocalSteamId) continue;

			Steam.PersonaState state = _steam.GetFriendPersonaState(friendId);
			if (state == Steam.PersonaState.Offline) continue;

			bool inThisGame = false;
			Godot.Collections.Dictionary played = _steam.GetFriendGamePlayed(friendId);
			if (played != null && played.TryGetValue("id", out Variant playedApp))
				inThisGame = playedApp.As<long>() == AppId;

			friends.Add(new SteamFriend(
				SteamId:        (ulong)friendId,
				Name:           PersonaNameFor((ulong)friendId),
				Presence:       ToPresence(state),
				InThisGame:     inThisGame,
				AlreadyInLobby: members.Contains((ulong)friendId)));
		}

		friends.Sort((a, b) =>
		{
			if (a.InThisGame != b.InThisGame) return a.InThisGame ? -1 : 1;
			if (a.Presence   != b.Presence)   return a.Presence.CompareTo(b.Presence);
			return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
		});

		return friends;
	}

	/// <summary>
	/// Invisible collapses onto Online: a friend showing as invisible to us is simply reachable, and there
	/// is nothing useful to tell the player about it.
	/// </summary>
	private static SteamPresence ToPresence(Steam.PersonaState state) => state switch
	{
		Steam.PersonaState.Busy            => SteamPresence.Busy,
		Steam.PersonaState.Away            => SteamPresence.Away,
		Steam.PersonaState.Snooze          => SteamPresence.Snooze,
		Steam.PersonaState.LookingToTrade  => SteamPresence.LookingToPlay,
		Steam.PersonaState.LookingToPlay   => SteamPresence.LookingToPlay,
		_                                  => SteamPresence.Online,
	};

	private readonly Dictionary<ulong, ImageTexture> _avatarCache = new();

	/// <summary>
	/// A user's small (32px) avatar, or null when Steam has not downloaded it yet —
	/// <see cref="AvatarUpdated"/> fires for that user once it arrives, so callers can ask again.
	/// Cached, so rebuilding a friends list does not re-decode every image.
	/// </summary>
	public ImageTexture SmallAvatarFor(ulong steamId)
	{
		if (!_available || steamId == 0) return null;
		if (_avatarCache.TryGetValue(steamId, out ImageTexture cached)) return cached;

		long handle = _steam.GetSmallFriendAvatar((long)steamId);
		if (handle == 0)
		{
			// Not in the local cache. Asking for it starts the download; the answer comes back on
			// AvatarLoadedSignal rather than from this call.
			_steam.GetPlayerAvatar((long)Steam.AvatarSizes.Small, (long)steamId);
			return null;
		}

		ImageTexture texture = BuildAvatarTexture(handle);
		if (texture != null) _avatarCache[steamId] = texture;
		return texture;
	}

	/// <summary>
	/// Turns a Steam image handle into a texture. Every field is checked rather than assumed: these two
	/// dictionaries come straight from the GDExtension, and a missing key would otherwise throw from
	/// inside a UI rebuild.
	/// </summary>
	private ImageTexture BuildAvatarTexture(long handle)
	{
		Godot.Collections.Dictionary size = _steam.GetImageSize(handle);
		Godot.Collections.Dictionary rgba = _steam.GetImageRgba(handle);
		if (size == null || rgba == null) return null;

		if (!size.TryGetValue("width",  out Variant widthValue))  return null;
		if (!size.TryGetValue("height", out Variant heightValue)) return null;
		if (!rgba.TryGetValue("buffer", out Variant bufferValue)) return null;

		int    width  = widthValue.As<int>();
		int    height = heightValue.As<int>();
		byte[] buffer = bufferValue.As<byte[]>();

		if (width <= 0 || height <= 0 || buffer == null || buffer.Length < width * height * 4) return null;

		Image image = Image.CreateFromData(width, height, false, Image.Format.Rgba8, buffer);
		return image == null ? null : ImageTexture.CreateFromImage(image);
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Steam callbacks
	// ══════════════════════════════════════════════════════════════════════════

	private void OnLobbyCreated(long result, long lobbyId)
	{
		// result is EResult: 1 == k_EResultOK.
		DebugUtilities.PrintPeerFinest($"Steam: lobby_created result={result} lobby={lobbyId}");
		_pendingCreate?.TrySetResult(result == (long)Steam.Result.Ok ? lobbyId : 0);
	}

	private void OnLobbyJoined(long lobbyId, long permissions, bool locked, long response)
	{
		DebugUtilities.PrintPeerFinest($"Steam: lobby_joined lobby={lobbyId} response={response}");
		_pendingJoin?.TrySetResult(response);
	}

	private void OnLobbyDataUpdate(long success, long lobbyId, long memberId)
	{
		// memberId == lobbyId marks a lobby-level update; member-level updates are not interesting here.
		if (memberId != lobbyId) return;
		if (_pendingLobbyData.TryGetValue(lobbyId, out TaskCompletionSource<bool> source))
			source.TrySetResult(success != 0);
	}

	private void OnLobbyChatUpdate(long lobbyId, long changedId, long makingChangeId, long chatState)
	{
		if (lobbyId == CurrentLobbyId)
			LobbyMembershipChanged?.Invoke();
	}

	private void OnJoinRequested(long lobbyId, long steamId)
	{
		DebugUtilities.PrintPeer($"Steam: join requested for lobby {lobbyId}");
		JoinRequested?.Invoke(lobbyId);
	}

	/// <summary>
	/// The signal's own payload is deliberately ignored. It carries the pixels as a
	/// <c>Godot.Collections.Array</c>, whose element typing is the fragile part of the generated bindings;
	/// re-reading the handle through <see cref="BuildAvatarTexture"/> takes the one path that is already
	/// proven. All this does is invalidate the cache and tell listeners to ask again.
	/// </summary>
	private void OnAvatarLoaded(long avatarId, long size, Godot.Collections.Array data)
	{
		_avatarCache.Remove((ulong)avatarId);
		AvatarUpdated?.Invoke((ulong)avatarId);
	}

	private void OnLobbyInvite(long inviter, long lobby, long game)
	{
		if (game != AppId) return;
		DebugUtilities.PrintPeer($"Steam: invited to lobby {lobby}");
		JoinRequested?.Invoke(lobby);
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Helpers
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Continuations must not run inline: these sources are completed from inside a Steam callback,
	/// and an inline continuation would re-enter Steamworks from within that callback.
	/// </summary>
	private static TaskCompletionSource<T> NewSource<T>()
		=> new(TaskCreationOptions.RunContinuationsAsynchronously);

	/// <summary>
	/// Resolves <paramref name="source"/> with <paramref name="fallback"/> if Steam has not answered
	/// in time, so a dropped callback surfaces as a normal failure instead of an await that never returns.
	/// </summary>
	private Task<T> WithTimeout<T>(TaskCompletionSource<T> source, T fallback, double seconds)
	{
		SceneTreeTimer timer = GetTree().CreateTimer(seconds);
		timer.Timeout += () => source.TrySetResult(fallback);
		return source.Task;
	}

	private static string DescribeEnterResponse(long response) => (Steam.ChatRoomEnterResponse)response switch
	{
		Steam.ChatRoomEnterResponse.DoesntExist      => "that game has already started or was closed.",
		Steam.ChatRoomEnterResponse.NotAllowed       => "you need an invite to join that game.",
		Steam.ChatRoomEnterResponse.Full             => "that game is full.",
		Steam.ChatRoomEnterResponse.Banned           => "you are banned from that game.",
		Steam.ChatRoomEnterResponse.Limited          => "your Steam account is limited.",
		Steam.ChatRoomEnterResponse.MemberBlockedYou => "a player in that game has blocked you.",
		Steam.ChatRoomEnterResponse.YouBlockedMember => "you have blocked a player in that game.",
		0                                            => "Steam did not respond in time.",
		_                                            => $"Steam refused the join ({response}).",
	};
}
