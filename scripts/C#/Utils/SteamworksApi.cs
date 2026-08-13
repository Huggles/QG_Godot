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

	/// <summary>Opens the Steam overlay invite dialog. Silently no-ops in the editor.</summary>
	public void OpenInviteOverlay()
	{
		if (!_available || CurrentLobbyId == 0) return;
		_steam.ActivateGameOverlayInviteDialog(CurrentLobbyId);
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
