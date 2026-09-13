using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static FactionMenuVisuals;

/// <summary>
/// Multiplayer lobby: host/join + per-player faction selection.
/// Each player picks factions from one team (Axis or Allies), or takes "Random" instead — no factions
/// named, and whatever nobody else claimed is dealt to them (still all on one team) when the game starts.
/// Host can revoke any player's faction assignment or Random claim.
/// All 6 factions must be accounted for — named or covered by a Random claim — before the game can start.
/// </summary>
public partial class MultiplayerLobby : Control
{
	/// <summary>Shared so JoinGameScreen and GameSettings use one source of truth for the port.</summary>
	public const int DEFAULT_PORT = 7777;
	private const string DEFAULT_SERVER_IP = "127.0.0.1";

	// ── Scene node references ─────────────────────────────────────────────────
	private VBoxContainer _playerListContainer;
	private Button        _startGameButton;
	/// <summary>Authored hidden in the .tscn: only a Steam host ever has anyone to invite.</summary>
	private Button        _inviteButton;
	private Label         _statusLabel;
	private OptionButton  _scenarioPicker;
	private RichTextLabel _scenarioDescriptionLabel;
	private LineEdit      _seedInput;
	private Button        _randomizeSeedButton;
	private CheckBox      _openingDiscardCheckBox;

	// ── Runtime state ─────────────────────────────────────────────────────────
	private bool _isHost        = false;
	private bool _lobbyOnlyMode = false;

	/// <summary>
	/// Set the moment the game start is committed, and never cleared: this node is on its way out.
	/// The scene change is deferred (and on the host waits a frame for the loading cover), so the
	/// lobby stays alive and connected for a short window afterwards — long enough to still receive
	/// peer signals. Broadcasting lobby state from that window sends packets addressed to a node the
	/// receiver has already replaced, which is where the "Node not found: GameRoot/MultiplayerLobby"
	/// RPC errors come from.
	/// </summary>
	private bool _gameStarting = false;

	/// <summary>True when this session rides on a Steam lobby rather than a direct ENet connection.</summary>
	private bool _isSteamSession = false;
	/// <summary>The Steam lobby backing this session; 0 for an ENet session.</summary>
	private long _steamLobbyId   = 0;

	/// <summary>True when this instance is a dedicated/headless server: it auto-hosts, controls no
	/// faction, and starts the game automatically once <see cref="_requiredPlayers"/> clients connect.</summary>
	private bool _dedicatedServer = false;
	/// <summary>Number of connecting clients a dedicated server waits for before starting (cmdline <c>players=N</c>).</summary>
	private int  _requiredPlayers = 2;

	/// <summary>peerId → name Label; text is also used as source for SyncPlayerList RPC.</summary>
	private readonly Dictionary<int, Label>          _playerLabels    = new();
	/// <summary>
	/// peerId → the name that peer reported for itself, WITHOUT the " (You)"/" (Host)" decoration the
	/// row label carries — which is why this cannot just read <see cref="_playerLabels"/>. Carried into
	/// the game on <see cref="PlayerFactionAssignment.DisplayName"/> by <see cref="StartGame"/>.
	///
	/// Absent for a peer that has not reported one yet: the "Player N" that <see cref="OnPeerConnected"/>
	/// puts in the row is a host-invented placeholder, not a name, and showing it in game would be a lie
	/// about who is playing. Only the HOST's copy matters — StartMultiplayerSession and StartNew both
	/// return early on a client, so only the host's list ever reaches LoadPlayers on any peer.
	/// </summary>
	private readonly Dictionary<int, string>         _playerNames     = new();
	/// <summary>peerId → root PanelContainer of that player's row.</summary>
	private readonly Dictionary<int, PanelContainer> _playerRowPanels = new();
	/// <summary>peerId → { faction → TextureButton }.</summary>
	private readonly Dictionary<int, Dictionary<Faction, TextureButton>> _factionButtons = new();
	/// <summary>peerId → that row's "Random" button.</summary>
	private readonly Dictionary<int, TextureButton> _randomButtons = new();
	/// <summary>Authoritative faction assignments: faction → owning peerId.</summary>
	private readonly Dictionary<Faction, int> _assignments = new();

	/// <summary>
	/// The save this lobby is restoring, or null for an ordinary lobby. Host-only — a client learns
	/// everything it needs through the sync RPCs that already exist, and never replays anything itself.
	/// </summary>
	private SaveGame _restoreSave;

	/// <summary>
	/// Factions a human has toggled since the pre-fill. ClaimSavedSeats never touches these again, which
	/// is the whole mechanism behind "pre-filled from the save, but still yours to change".
	/// </summary>
	private readonly HashSet<Faction> _manuallyAssigned = new();

	/// <summary>Saved seat index to peer id, rebuilt from scratch on every seat event.</summary>
	private readonly Dictionary<int, int> _seatOwners = new();

	private Label _restoreLabel;
	/// <summary>
	/// Authoritative set of peers that picked "Random" rather than naming factions. Turned into real
	/// entries in <see cref="_assignments"/> by <see cref="DealRandomClaims"/> when the host starts the
	/// game, and not a moment earlier — the whole point is that nobody knows what they drew until then.
	///
	/// Per row this is mutually exclusive with holding factions; see <see cref="RequestRandomToggle"/>.
	/// </summary>
	private readonly HashSet<int> _randomClaims = new();

	// ══════════════════════════════════════════════════════════════════════════
	// Godot lifecycle
	// ══════════════════════════════════════════════════════════════════════════

	// Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
	// surfacing rather than a silent console line.
	public override void _Ready() => Guard.Try(ReadyInternal, "MultiplayerLobby._Ready");

	private void ReadyInternal()
	{
		DebugUtilities.PrintPeerFinest("MultiplayerLobby: Ready");
		
		// Get UI references
		_playerListContainer = GetNode<VBoxContainer>("%PlayerListContainer");
		_startGameButton     = GetNode<Button>("%StartGameButton");
		_inviteButton        = GetNode<Button>("%InviteButton");
		_statusLabel         = GetNode<Label>("%StatusLabel");
		_scenarioPicker      = GetNode<OptionButton>("%ScenarioOptionButton");
		_scenarioDescriptionLabel = GetNode<RichTextLabel>("%ScenarioDescriptionLabel");
		_seedInput           = GetNode<LineEdit>("%SeedInput");
		_randomizeSeedButton = GetNode<Button>("%RandomizeSeedButton");
		_openingDiscardCheckBox = GetNode<CheckBox>("%OpeningDiscardCheckBox");
		_restoreLabel        = GetNode<Label>("%RestoreLabel");

		// Read before anything else touches MainMenu.ClearLobbyIntent — which this method calls on itself
		// further down, and which must therefore never be the thing that clears PendingSave.
		_restoreSave = GameManager.PendingSave ?? ArmDebugSaveFromCommandLine();

		_startGameButton.Visible = false;

		_startGameButton.Pressed += OnStartGameButtonPressed;
		_inviteButton.Pressed    += OnInviteFriendsPressed;

		Multiplayer.PeerConnected      += OnPeerConnected;
		Multiplayer.PeerDisconnected   += OnPeerDisconnected;
		Multiplayer.ConnectedToServer  += OnConnectedToServer;
		Multiplayer.ConnectionFailed   += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
		
		UpdateStatusLabel("Waiting to host or join...");

		var gameManager = GetNode<GameManager>("/root/GameManager");

		// Tutorials are excluded here and nowhere else: a tutorial drives five factions from a script,
		// and TutorialRuntime refuses to start one with peers attached, so offering it in a lobby can
		// only produce a game nobody chose to play that way. The Skirmish screen keeps them.
		MenuScenarioPicker.Populate(_scenarioPicker, gameManager.AvailableScenarios, includeTutorials: false);

		if (gameManager.SelectedScenario != null)
		{
			// A selection this picker cannot show is a real case now that it filters: the player may
			// have chosen a tutorial on the Skirmish screen and come here instead, leaving the static
			// pointing at something with no item. Falling back only in the WIDGET would leave the two
			// disagreeing and silently start the tutorial scenario, so the fallback is pushed into the
			// static as well.
			//
			// Not while restoring: that branch below replaces the picker wholesale with the save's own
			// scenario, and SetSelectedScenarioByIndex would also drop PendingScenarioJson, which the
			// restore still needs.
			bool shown = MenuScenarioPicker.SelectByPath(
				_scenarioPicker, gameManager.AvailableScenarios, gameManager.SelectedScenario.Path);

			if (!shown && _restoreSave == null && _scenarioPicker.ItemCount > 0)
			{
				_scenarioPicker.Selected = 0;
				gameManager.SetSelectedScenarioByIndex(MenuScenarioPicker.ScenarioIndexAt(_scenarioPicker, 0));
			}

			UpdateScenarioDescription(gameManager.SelectedScenario.Description);
		}
		else
		{
			UpdateScenarioDescription(MenuScenarioPicker.NoScenariosMessage);
		}

		// Restoring: the scenario came with the save, so nothing is resolved against the local list —
		// the file may have been renamed, edited or deleted since. Show its title as a one-off entry and
		// let the picker stay locked.
		if (_restoreSave != null)
		{
			_scenarioPicker.Clear();
			// id -1: this item resolves against no local scenario, so ScenarioIndexAt answers -1 and the
			// handlers return early. Defensive — the picker is Disabled on this path — but it keeps the
			// invariant "an item's id is its AvailableScenarios index" true with no exceptions.
			_scenarioPicker.AddItem(_restoreSave.ScenarioTitle ?? "Saved scenario", -1);
			_scenarioPicker.Selected = 0;
			UpdateScenarioDescription("This scenario is stored inside the save and cannot be changed.");
		}

		_scenarioPicker.Disabled = true;
		_scenarioPicker.ItemSelected += OnScenarioSelected;

		// Only the host's seed is ever used (MultiplayerSession.StartNew reads it on the server and
		// RPCs the value to every peer), so the box is locked alongside the scenario picker until
		// this peer turns out to be the host. Clients still see the field, greyed out.
		MenuSeedField.Bind(_seedInput, _randomizeSeedButton);

		// AFTER Bind, which overwrites the field with a freshly rolled seed. A restore must run on the
		// seed the save records: the log carries the deck order the original shuffle produced, and a
		// different stream would only diverge visibly several turns later.
		if (_restoreSave != null) _seedInput.Text = _restoreSave.Seed.ToString();

		SetSeedFieldEnabled(false);

		// Same story again: host-only, and its starting value is whatever the selected scenario says.
		// Set before subscribing, so seeding the box does not look like the host toggling it.
		_openingDiscardCheckBox.ButtonPressed = _restoreSave?.OpeningDiscard
			?? gameManager.SelectedScenario?.OpeningDiscard ?? true;
		_openingDiscardCheckBox.Disabled = true;
		_openingDiscardCheckBox.Toggled += OnOpeningDiscardToggled;

		// Arrived from JoinGameScreen, which already established the client connection.
		// Adopt it rather than creating a second peer.
		// This branch is transport-agnostic: a connected SteamMultiplayerPeer satisfies it exactly as
		// an ENet client peer does, so joining over Steam needs no separate case here.
		if (Multiplayer.MultiplayerPeer != null
			&& Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected
			&& !Multiplayer.IsServer())
		{
			// Only the host restores. A client rebuilds the game from the broadcast stream exactly as it
			// does for a fresh one, and never sees the save at all.
			_restoreSave = null;

			_isSteamSession = MainMenu.PendingLobbyIntent == MainMenu.LobbyIntent.JoinSteam;
			_steamLobbyId   = MainMenu.PendingSteamLobbyId;
			MainMenu.ClearLobbyIntent();

			int me = Multiplayer.GetUniqueId();
			UpdateStatusLabel("Connected – waiting for lobby data...");
			AddPlayerRow(me, $"{LocalDisplayName()} (You)", LocalDisplayName());
			// Deferred so this node has finished entering the tree before the RPC goes out.
			CallDeferred(nameof(RequestLobbyStateFromHost));
			return;
		}

		_lobbyOnlyMode = OS.GetCmdlineUserArgs().Contains("lobby_only=true");
		int instance   = GetInstanceNumber();

		// Dedicated/headless server: auto-host and wait for clients. It controls no faction.
		// Gated on IsDedicatedServer, not IsHeadless: a CLI run is also headless but never reaches
		// here (MainScene routes it to CliBootstrap), and must not auto-host.
		if (GameContext.IsDedicatedServer)
		{
			_dedicatedServer = true;
			_requiredPlayers = GetIntArg("players", 2);
			DebugUtilities.PrintPeer($"Dedicated server: auto-hosting, waiting for {_requiredPlayers} client(s)...");
			StartGodotHost();
			return;
		}

		// GUI test client that should connect to a dedicated server without menu interaction.
		if (OS.GetCmdlineUserArgs().Contains("auto_join=true"))
		{
			JoinAt(DEFAULT_SERVER_IP);
			return;
		}

		if (GameSettings.IsDebugMultiplayer && instance == 1)
		{
			DebugUtilities.PrintPeer(_lobbyOnlyMode
				? "DebugMultiplayer Instance 1: auto-hosting (lobby only)..."
				: "DebugMultiplayer Instance 1: auto-hosting...");
			StartGodotHost();
		}
		else if (GameSettings.IsDebugMultiplayer && instance == 2)
		{
			DebugUtilities.PrintPeer(_lobbyOnlyMode
				? "DebugMultiplayer Instance 2: auto-joining (lobby only)..."
				: "DebugMultiplayer Instance 2: auto-joining...");
			JoinAt(DEFAULT_SERVER_IP);
		}
		else if (MainMenu.PendingLobbyIntent == MainMenu.LobbyIntent.HostGodot)
		{
			MainMenu.ClearLobbyIntent();
			StartGodotHost();
		}
		else if (MainMenu.PendingLobbyIntent == MainMenu.LobbyIntent.HostSteam)
		{
			long lobbyId = MainMenu.PendingSteamLobbyId;
			MainMenu.ClearLobbyIntent();
			StartSteamHost(lobbyId);
		}
	}

	/// <summary>
	/// Honour a <c>load=</c> command-line argument, so a debug launch can go straight into a restored
	/// game with no clicking — the same affordance <c>scenario=</c> gives a fresh one.
	///
	/// Worth having beyond convenience: unit placement is view state that lives inside an animation a
	/// restore suppresses (see IWorldPresenter.PlaceDeployedUnits), and that class of bug is invisible to
	/// a headless run. This is how you look at a restored board without playing a game to get there.
	/// </summary>
	private static SaveGame ArmDebugSaveFromCommandLine()
	{
		string path = CliArgs.Get("load");
		if (string.IsNullOrEmpty(path)) return null;

		SaveGame save = SaveGameService.Load(path);
		if (save == null)
		{
			DebugUtilities.PrintPeerError($"Lobby: could not read the save at '{path}'; starting a fresh game");
			return null;
		}

		GameManager.ArmRestore(save);
		DebugUtilities.PrintPeer($"Lobby: debug launch restoring '{save.DisplayName}' from {path}");
		return save;
	}

	private void RequestLobbyStateFromHost()
	{
		RpcId(1, nameof(ReportPlayerName), LocalDisplayName());
		RpcId(1, nameof(RequestLobbyState));
	}

	/// <summary>
	/// What to call this player in the lobby list. Kept transport-agnostic on purpose: reading the
	/// Steam persona works for an ENet session too whenever Steam happens to be running, and the
	/// lobby never has to ask the peer what kind of transport it is.
	/// </summary>
	private string LocalDisplayName()
		=> SteamworksApi.IsAvailable
			? SteamworksApi.Instance.LocalPersonaName
			: $"Player {Multiplayer.GetUniqueId()}";

	/// <summary>
	/// Client → host, alongside RequestLobbyState. The host cannot work a client's persona name out
	/// for itself, so each client reports its own and the host re-broadcasts the updated list.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void ReportPlayerName(string displayName)
	{
		if (!Multiplayer.IsServer() || _gameStarting) return;

		int sender = Multiplayer.GetRemoteSenderId();
		if (!_playerLabels.TryGetValue(sender, out Label label)) return;

		string clean = MakeNameUnique(SanitisePlayerName(displayName, sender), sender);
		label.Text            = clean;
		_playerNames[sender]  = clean;
		DebugUtilities.PrintPeer($"Peer {sender} reported \"{displayName}\", listed as \"{clean}\"");
		Rpc(nameof(SyncPlayerList), GetPlayerListData());

		// The load-bearing seating hook: this is the first moment the host knows who a peer actually is,
		// and therefore the first moment a saved seat can be matched to them by name.
		ReseatForRestore();
	}

	/// <summary>
	/// Appends " #2", " #3", … to a name another peer is already using, so identically-named players
	/// stay tellable apart in the lobby and on the assignment carried into the game.
	///
	/// Not a debug-only concern, though it is loudest there: every local test instance reads the same
	/// Steam persona (see <see cref="LocalDisplayName"/>), so a six-instance lobby is otherwise six
	/// rows with one name on them. Two real players can just as easily share a persona name.
	///
	/// Host-only, and that is what makes it correct: every name passes through
	/// <see cref="ReportPlayerName"/> here before it reaches anyone's list, so the host's view of who
	/// is called what is the only one that has to be consistent. The peer keeps whichever name it
	/// first claimed — the suffix lands on whoever reports later, and the host itself, having joined
	/// first, always keeps the undecorated name.
	/// </summary>
	private string MakeNameUnique(string desiredName, int forPeerId)
	{
		bool TakenByAnother(string candidate)
			=> _playerNames.Any(kv => kv.Key != forPeerId && kv.Value == candidate);

		if (!TakenByAnother(desiredName)) return desiredName;

		// Bounded by the peer count: a lobby only ever holds a host plus six clients.
		for (int suffix = 2; suffix <= _playerNames.Count + 2; suffix++)
		{
			string candidate = $"{desiredName} #{suffix}";
			if (!TakenByAnother(candidate)) return candidate;
		}

		// Unreachable while peer ids are unique, but it keeps the method total.
		return $"{desiredName} #{forPeerId}";
	}

	/// <summary>
	/// The name arrives from another machine, so it is untrusted: cap the length and drop control
	/// characters so one player cannot garble or blow out everyone else's lobby row.
	/// </summary>
	private static string SanitisePlayerName(string raw, int peerId)
	{
		if (string.IsNullOrWhiteSpace(raw)) return $"Player {peerId}";

		var cleaned = new System.Text.StringBuilder(raw.Length);
		foreach (char c in raw)
		{
			if (char.IsControl(c)) continue;
			cleaned.Append(c);
			if (cleaned.Length >= 32) break;
		}

		string result = cleaned.ToString().Trim();
		return result.Length == 0 ? $"Player {peerId}" : result;
	}

	private static int GetInstanceNumber()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("instance=") && int.TryParse(arg.Substring("instance=".Length), out int n))
				return n;
		}
		return 0;
	}

	/// <summary>Parses a <c>key=value</c> integer command-line user arg, or returns <paramref name="fallback"/>.</summary>
	private static int GetIntArg(string key, int fallback)
	{
		string prefix = $"{key}=";
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith(prefix) && int.TryParse(arg.Substring(prefix.Length), out int n))
				return n;
		}
		return fallback;
	}

	public override void _ExitTree()
	{
		Multiplayer.PeerConnected      -= OnPeerConnected;
		Multiplayer.PeerDisconnected   -= OnPeerDisconnected;
		Multiplayer.ConnectedToServer  -= OnConnectedToServer;
		Multiplayer.ConnectionFailed   -= OnConnectionFailed;
		Multiplayer.ServerDisconnected -= OnServerDisconnected;
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Connecting
	//
	// Nothing on this screen starts a connection any more: hosting and joining are both chosen before
	// the player arrives here (MainMenu's host dialog, JoinGameScreen, SteamFriendLobbiesScreen). These
	// are driven by ReadyInternal from the lobby intent, the dedicated-server path, and the debug
	// auto-host/join instances.
	// ══════════════════════════════════════════════════════════════════════════

	private void StartGodotHost()
	{
		DebugUtilities.PrintPeer("Starting host...");
		
		var peer  = new ENetMultiplayerPeer();
		Error err = peer.CreateServer(DEFAULT_PORT, 6);
		if (err != Error.Ok)
		{
			DebugUtilities.PrintPeerError($"Failed to create server: {err}");
			UpdateStatusLabel($"Failed to host: {err}");
			return;
		}
		
		Multiplayer.MultiplayerPeer = peer;
		_isHost                     = true;
		DebugUtilities.PrintPeer($"Server started on port {DEFAULT_PORT}");
		UpdateStatusLabel($"Hosting on port {DEFAULT_PORT}");

		EnterHostUiState();
	}

	/// <summary>
	/// Hosts on an already-created Steam lobby. The lobby is created back on the main menu, before
	/// navigating here, because the peer requires this client to already own it.
	/// </summary>
	private void StartSteamHost(long lobbyId)
	{
		DebugUtilities.PrintPeer($"Starting Steam host on lobby {lobbyId}...");

		MultiplayerPeer peer = SteamPeerFactory.CreateHost(lobbyId, out string error);
		if (peer == null)
		{
			DebugUtilities.PrintPeerError($"Failed to host via Steam: {error}");
			UpdateStatusLabel($"Failed to host via Steam — {error}");
			SteamworksApi.Instance?.LeaveCurrentLobby();
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
		_isHost         = true;
		_isSteamSession = true;
		_steamLobbyId   = lobbyId;

		DebugUtilities.PrintPeer($"Steam host started on lobby {lobbyId}");
		UpdateStatusLabel($"Hosting via Steam as {LocalDisplayName()}");

		EnterHostUiState();
		_inviteButton.Visible = true;
	}

	/// <summary>Shared by both host paths, so the ENet flow keeps behaving exactly as it did.</summary>
	/// <summary>
	/// Both host paths converge here, so it is the only place the host-editable controls need gating.
	/// When a save is being restored all three of them come from the save and stay locked.
	/// </summary>
	private void EnterHostUiState()
	{
		AddPlayerRow(1, $"{LocalDisplayName()} (Host)", LocalDisplayName());
		_startGameButton.Visible  = true;
		_startGameButton.Disabled = true; // unlocks once all 6 factions are assigned

		bool restoring = _restoreSave != null;
		_scenarioPicker.Disabled  = restoring;
		SetSeedFieldEnabled(!restoring);
		_openingDiscardCheckBox.Disabled = restoring;

		if (!restoring) return;

		ShowRestoreBanner(RestoreBannerText());
		Rpc(nameof(SyncRestoreBanner), RestoreBannerText());
		ClaimSavedSeats();
		BroadcastFactionState();
	}


	// ══════════════════════════════════════════════════════════════════════════
	// Restoring a save
	// ══════════════════════════════════════════════════════════════════════════

	private string RestoreBannerText()
		=> $"Restoring \"{_restoreSave.DisplayName}\" — assign factions, then Start.";

	private void ShowRestoreBanner(string text)
	{
		_restoreLabel.Text    = text;
		_restoreLabel.Visible = !string.IsNullOrEmpty(text);
	}

	/// <summary>
	/// Tells clients a save is being restored and what it is. They cannot work it out for themselves:
	/// the save never leaves the host, and the scenario is not in their AvailableScenarios list.
	/// An empty string means "not restoring".
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void SyncRestoreBanner(string text) => ShowRestoreBanner(text);

	/// <summary>
	/// Hand each connected peer the seat it held in the saved game: matched on display name first, then
	/// by join order, with everything left over folding onto the host. A solo restore therefore needs no
	/// clicks, and a short-handed one is still startable.
	///
	/// Host-only, idempotent, and re-run on every seat event. It never touches a faction someone has
	/// toggled since (see <see cref="_manuallyAssigned"/>), which is what makes the pre-fill a
	/// suggestion rather than a lock.
	///
	/// Worth being clear about why this is only a convenience: the replay runs entirely on the host and
	/// is keyed to nothing about peers. Who controls which faction now cannot change whether the board
	/// reconstructs — only who gets asked the questions afterwards. That is also why locking the faction
	/// grid to the saved layout would be the wrong call.
	/// </summary>
	private void ClaimSavedSeats()
	{
		if (_restoreSave == null || !Multiplayer.IsServer() || _gameStarting) return;

		List<int> peers = _playerLabels.Keys.OrderBy(peerId => peerId).ToList();
		List<SavedSeat> seats = _restoreSave.Seats;
		HashSet<int> seated = new();

		_seatOwners.Clear();

		// Pass A — by name. Matched on the name before MakeNameUnique got at it: every local test
		// instance reports the same Steam persona, so saved names routinely carry a " #2" that the same
		// player will not necessarily be given again.
		for (int i = 0; i < seats.Count; i++)
		{
			int match = peers.FirstOrDefault(
				peerId => !seated.Contains(peerId) && NamesMatch(PlayerNameFor(peerId), seats[i].DisplayName),
				-1);

			if (match == -1) continue;
			_seatOwners[i] = match;
			seated.Add(match);
		}

		// Pass B — by join order, for anyone the names did not place.
		for (int i = 0; i < seats.Count; i++)
		{
			if (_seatOwners.ContainsKey(i)) continue;

			int next = peers.FirstOrDefault(peerId => !seated.Contains(peerId), -1);
			if (next == -1) break;

			_seatOwners[i] = next;
			seated.Add(next);
		}

		// Pass C — the host absorbs every seat nobody turned up for, so all six factions stay covered
		// and Start still unlocks with fewer players than the original game had.
		for (int i = 0; i < seats.Count; i++)
			if (!_seatOwners.ContainsKey(i)) _seatOwners[i] = 1;

		foreach (KeyValuePair<int, int> seat in _seatOwners)
			foreach (Faction faction in seats[seat.Key].Factions)
				if (!_manuallyAssigned.Contains(faction))
					_assignments[faction] = seat.Value;

		// Random and named factions are mutually exclusive per row everywhere else; leaving both set
		// would put the row's two refresh paths in contradictory states.
		_randomClaims.RemoveWhere(peerId => _assignments.ContainsValue(peerId));

		DebugUtilities.PrintPeer(
			$"Restore: seated {_seatOwners.Count} saved seat(s) across {peers.Count} player(s)");
	}

	private string PlayerNameFor(int peerId)
		=> _playerNames.TryGetValue(peerId, out string name) ? name : null;

	/// <summary>
	/// Compares two lobby names ignoring the " #2" / " #3" suffix MakeNameUnique appends, since which
	/// duplicate gets the suffix depends on join order and can differ between the save and the restore.
	/// </summary>
	private static bool NamesMatch(string a, string b)
	{
		if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
		return string.Equals(StripDuplicateSuffix(a), StripDuplicateSuffix(b),
			StringComparison.OrdinalIgnoreCase);
	}

	private static string StripDuplicateSuffix(string name)
	{
		int hash = name.LastIndexOf(" #", StringComparison.Ordinal);
		if (hash <= 0) return name;

		string tail = name[(hash + 2)..];
		return tail.Length > 0 && tail.All(char.IsDigit) ? name[..hash] : name;
	}

	/// <summary>
	/// Re-seat and re-broadcast after anything that changes who is in the lobby. A no-op when not
	/// restoring, so the call sites do not need to ask.
	/// </summary>
	private void ReseatForRestore()
	{
		if (_restoreSave == null || !Multiplayer.IsServer() || _gameStarting) return;
		ClaimSavedSeats();
		BroadcastFactionState();
	}
	private void OnInviteFriendsPressed() => InviteFriendsDialog.Show(this);

	private void JoinAt(string ip)
	{
		DebugUtilities.PrintPeer("Joining server...");

		var peer  = new ENetMultiplayerPeer();
		Error err = peer.CreateClient(ip, DEFAULT_PORT);
		if (err != Error.Ok)
		{
			DebugUtilities.PrintPeerError($"Failed to create client: {err}");
			UpdateStatusLabel($"Failed to join: {err}");
			return;
		}
		
		Multiplayer.MultiplayerPeer = peer;
		DebugUtilities.PrintPeer($"Connecting to {ip}:{DEFAULT_PORT}");
		UpdateStatusLabel($"Connecting to {ip}:{DEFAULT_PORT}...");
	}

	private void OnStartGameButtonPressed()
	{
		if (!_isHost)
		{
			DebugUtilities.PrintPeerError("Only the host can start the game");
			return;
		}
		if (!CanCoverAllFactions(out string problem))
		{
			DebugUtilities.PrintPeerError($"Cannot start: {problem}");
			return;
		}
		DebugUtilities.PrintPeer("Host starting game...");

		// The draw happens here, on the host, and is pushed out as a plain assignment sync before the
		// start goes out — StartGame reads _assignments on every peer, so the table has to be settled
		// and identical everywhere first. RPCs are reliable and ordered, so the sync lands first.
		if (_randomClaims.Count > 0)
		{
			DealRandomClaims();
			BroadcastFactionState();
		}

		if (_restoreSave != null)
		{
			// All of it comes from the save, not from the (locked, but still populated) fields. The replay
			// only reproduces the original game if the scenario, the seed and the opening deal are the
			// ones it was recorded against.
			GameManager.PendingSave           = _restoreSave;
			GameManager.PendingSeed           = _restoreSave.Seed;
			GameManager.PendingOpeningDiscard = _restoreSave.OpeningDiscard;
		}
		else
		{
			// Only the host's field is read: the seed travels to the clients on the StartSession RPC,
			// so a client's own box never affects its game.
			MenuSeedField.Commit(_seedInput);
			CommitOpeningDiscard();
		}

		Rpc(nameof(StartGame));
	}

	/// <summary>
	/// Hand the host's toggle to the game, the same way MenuSeedField.Commit hands over the seed —
	/// and for the same reason: SetupInitialGameState runs on the host only, so only the host's box
	/// matters and no RPC is needed to carry it.
	/// </summary>
	private void CommitOpeningDiscard()
		=> GameManager.PendingOpeningDiscard = _openingDiscardCheckBox.ButtonPressed; 

	// ══════════════════════════════════════════════════════════════════════════
	// Faction selection — client side
	// ══════════════════════════════════════════════════════════════════════════

	private void OnFactionButtonPressed(int rowPeerId, Faction faction)
	{
		int me = Multiplayer.GetUniqueId();
		// Only the row's owner or the host may interact with a row.
		if (me != rowPeerId && me != 1) return;

		if (Multiplayer.IsServer())
			RequestFactionToggle(rowPeerId, (int)faction);
		else
			RpcId(1, nameof(RequestFactionToggle), rowPeerId, (int)faction);
	}

	private void OnRandomButtonPressed(int rowPeerId)
	{
		int me = Multiplayer.GetUniqueId();
		// Only the row's owner or the host may interact with a row.
		if (me != rowPeerId && me != 1) return;

		if (Multiplayer.IsServer())
			RequestRandomToggle(rowPeerId);
		else
			RpcId(1, nameof(RequestRandomToggle), rowPeerId);
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Faction selection — server side (RPCs)
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Called on the host (directly or via RPC from a client).
	/// Validates and applies the requested faction toggle, then broadcasts the new state.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RequestFactionToggle(int targetPeerId, int factionInt)
	{
		if (!Multiplayer.IsServer() || _gameStarting) return;

		int requester = (int)Multiplayer.GetRemoteSenderId();
		if (requester == 0) requester = 1; // direct (local) call by host

		Faction faction       = (Faction)factionInt;
		bool    isClaimed     = _assignments.TryGetValue(faction, out int owner);
		bool    ownedByTarget = isClaimed && owner == targetPeerId;

		if (requester == 1 && targetPeerId != 1)
		{
			// Host acting on another player's row: revoke only.
			if (ownedByTarget)
				_assignments.Remove(faction);
		}
		else if (requester == targetPeerId)
		{
			if (ownedByTarget)
			{
				// Unclaim own faction.
				_assignments.Remove(faction);
			}
			else if (!isClaimed && !_randomClaims.Contains(requester))
			{
				// Attempt to claim: enforce team constraint. A player holding Random cannot also name
				// factions — they gave up the choice, which is exactly what Random means.
				FactionTeam playerTeam  = GetTeam(requester);
				FactionTeam factionTeam = AxisSet.Contains(faction) ? FactionTeam.AXIS : FactionTeam.ALLIES;
				if (playerTeam == FactionTeam.NONE || playerTeam == factionTeam)
					_assignments[faction] = requester;
			}
		}

		// Whatever the outcome, a human has now had an opinion about this faction, so the saved seating
		// must stop reasserting itself over it. This is what makes the restore pre-fill a suggestion.
		_manuallyAssigned.Add(faction);

		BroadcastFactionState();
	}

	/// <summary>
	/// Called on the host (directly or via RPC from a client): toggles a row's "Random" claim.
	/// Mirrors <see cref="RequestFactionToggle"/>'s permission model exactly — a player toggles their
	/// own row, and the host can only revoke on someone else's.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RequestRandomToggle(int targetPeerId)
	{
		if (!Multiplayer.IsServer() || _gameStarting) return;

		int requester = (int)Multiplayer.GetRemoteSenderId();
		if (requester == 0) requester = 1; // direct (local) call by host

		bool heldByTarget = _randomClaims.Contains(targetPeerId);

		if (requester == 1 && targetPeerId != 1)
		{
			// Host acting on another player's row: revoke only.
			if (heldByTarget)
				_randomClaims.Remove(targetPeerId);
		}
		else if (requester == targetPeerId)
		{
			if (heldByTarget)
				_randomClaims.Remove(requester);
			// Random is all-or-nothing for a row: a player already holding factions has made their pick.
			else if (!_assignments.ContainsValue(requester))
				_randomClaims.Add(requester);
		}

		// Taking or dropping Random is a decision about that row as a whole, so nothing on it may be
		// re-seated from the save afterwards. See ClaimSavedSeats.
		foreach (Faction held in _assignments.Where(kv => kv.Value == targetPeerId).Select(kv => kv.Key).ToList())
			_manuallyAssigned.Add(held);

		BroadcastFactionState();
	}

	private void BroadcastFactionState()
		=> Rpc(nameof(SyncFactionState), SerialiseAssignments(), SerialiseRandomClaims());

	/// <summary>
	/// Broadcast from the host to all peers (and locally): the authoritative assignment table.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void SyncFactionState(Godot.Collections.Dictionary<int, int> data, int[] randomPeers)
	{
		_assignments.Clear();
		foreach (var kv in data)
			_assignments[(Faction)kv.Key] = kv.Value;

		_randomClaims.Clear();
		foreach (int peerId in randomPeers)
			_randomClaims.Add(peerId);

		RefreshAllButtons();
		UpdateStartGameButton();
	}

	private void RefreshAllButtons()
	{
		foreach (var (peerId, buttons) in _factionButtons)
			foreach (var (faction, btn) in buttons)
				RefreshButton(peerId, faction, btn);

		foreach (var (peerId, btn) in _randomButtons)
			RefreshRandomButton(peerId, btn);
	}

	private void RefreshButton(int rowPeerId, Faction faction, TextureButton btn)
	{
		int  me      = Multiplayer.GetUniqueId();
		bool isMyRow = rowPeerId == me;
		bool iAmHost = me == 1;

		bool isClaimed    = _assignments.TryGetValue(faction, out int owner);
		bool ownedByRow   = isClaimed && owner == rowPeerId;
		bool ownedByOther = isClaimed && !ownedByRow;

		FactionTeam rowTeam     = GetTeam(rowPeerId);
		bool        isAxis      = AxisSet.Contains(faction);
		FactionTeam factionTeam = isAxis ? FactionTeam.AXIS : FactionTeam.ALLIES;
		bool        teamOk      = rowTeam == FactionTeam.NONE || rowTeam == factionTeam;

		if (ownedByRow)
		{
			// Gold: this row's player has claimed this faction.
			// Clickable by the owner (to unclaim) or the host (to revoke).
			btn.Modulate = ColClaimed;
			btn.Disabled = !(isMyRow || iAmHost);
		}
		else if (ownedByOther)
		{
			// Another player already owns this faction.
			btn.Modulate = ColOtherOwned;
			btn.Disabled = true;
		}
		else if (!isMyRow)
		{
			// Viewing another player's row; unclaimed factions are neutral/inactive.
			btn.Modulate = ColOtherOwned;
			btn.Disabled = true;
		}
		else if (_randomClaims.Contains(rowPeerId))
		{
			// My row, but I picked Random: naming a faction is no longer mine to do.
			btn.Modulate = ColUnavailable;
			btn.Disabled = true;
		}
		else if (!teamOk)
		{
			// My row, but this faction belongs to the opposing team.
			btn.Modulate = ColUnavailable;
			btn.Disabled = true;
		}
		else
		{
			// My row, faction available and team-compatible.
			btn.Modulate = ColAvailable;
			btn.Disabled = false;
		}
	}

	/// <summary>
	/// Same four states as <see cref="RefreshButton"/>, read off <see cref="_randomClaims"/> instead:
	/// claimed by this row, another row's business, blocked because this row already holds factions,
	/// or free to take.
	/// </summary>
	private void RefreshRandomButton(int rowPeerId, TextureButton btn)
	{
		int  me      = Multiplayer.GetUniqueId();
		bool isMyRow = rowPeerId == me;
		bool iAmHost = me == 1;

		if (_randomClaims.Contains(rowPeerId))
		{
			// Gold: this row's player is going random.
			// Clickable by the owner (to unclaim) or the host (to revoke).
			btn.Modulate = ColClaimed;
			btn.Disabled = !(isMyRow || iAmHost);
		}
		else if (!isMyRow)
		{
			btn.Modulate = ColOtherOwned;
			btn.Disabled = true;
		}
		else if (_assignments.ContainsValue(me))
		{
			// My row, but I have already named factions — the two are mutually exclusive.
			btn.Modulate = ColUnavailable;
			btn.Disabled = true;
		}
		else
		{
			btn.Modulate = ColAvailable;
			btn.Disabled = false;
		}
	}

	private FactionTeam GetTeam(int peerId)
	{
		if (_assignments.Any(kv => kv.Value == peerId && AxisSet.Contains(kv.Key)))
			return FactionTeam.AXIS;
		if (_assignments.Any(kv => kv.Value == peerId && !AxisSet.Contains(kv.Key)))
			return FactionTeam.ALLIES;
		return FactionTeam.NONE;
	}

	private Godot.Collections.Dictionary<int, int> SerialiseAssignments()
	{
		var d = new Godot.Collections.Dictionary<int, int>();
		foreach (var kv in _assignments) d[(int)kv.Key] = kv.Value;
		return d;
	}

	/// <summary>
	/// A plain <c>int[]</c> — it goes over the wire as a PackedInt32Array, which needs no type
	/// information carried alongside it the way a typed Array would.
	/// </summary>
	private int[] SerialiseRandomClaims() => _randomClaims.ToArray();

	private void UpdateStartGameButton()
	{
		if (!_isHost) return;
		bool canStart = CanCoverAllFactions(out string problem);
		_startGameButton.Disabled    = !canStart;
		_startGameButton.TooltipText = canStart
			? "All factions accounted for – ready to start!"
			: problem;
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Random claims
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Whether every faction has an owner, counting a Random claim as cover for its share of whatever
	/// nobody named. Host-side gate for the start button, and the precondition
	/// <see cref="DealRandomClaims"/> is written against.
	///
	/// Two things can go wrong once Random is in play, and both are the host's to fix:
	/// there are more Random claims than factions left to hand out, or the leftovers straddle both
	/// teams with only one player willing to take them (a player only ever ends up on one team, so a
	/// lone Random pick cannot mop up an Axis and an Allied faction between them).
	/// </summary>
	private bool CanCoverAllFactions(out string problem)
	{
		List<Faction> unclaimed = AllPlayableFactions.Where(f => !_assignments.ContainsKey(f)).ToList();
		int           pickers   = _randomClaims.Count;

		if (pickers == 0)
		{
			problem = unclaimed.Count == 0 ? null : $"{unclaimed.Count} faction(s) still unassigned";
			return unclaimed.Count == 0;
		}

		if (unclaimed.Count == 0)
		{
			problem = $"{pickers} Random pick(s) with no factions left to draw from";
			return false;
		}
		if (pickers > unclaimed.Count)
		{
			problem = $"{pickers} Random pick(s) but only {unclaimed.Count} faction(s) left";
			return false;
		}

		int teamsInPool = (unclaimed.Any(AxisSet.Contains) ? 1 : 0)
						+ (unclaimed.Any(f => !AxisSet.Contains(f)) ? 1 : 0);
		if (pickers < teamsInPool)
		{
			problem = "Both teams have factions left, so one Random pick cannot cover them all";
			return false;
		}

		problem = null;
		return true;
	}

	/// <summary>
	/// Turns every Random claim into real ownership, dealt from the factions nobody named. Each picker
	/// comes out on a single team — the game has no notion of a player straddling both — and the
	/// factions already claimed by hand are, by construction, never in the pool.
	///
	/// Host-only, and called exactly once: the draw happens on the host and travels to the clients as
	/// an ordinary assignment sync, so nobody rolls their own and diverges.
	/// Assumes <see cref="CanCoverAllFactions"/> has just said yes.
	/// </summary>
	private void DealRandomClaims()
	{
		if (_randomClaims.Count == 0) return;

		var rng = new Random();
		var pool = new Dictionary<FactionTeam, List<Faction>>
		{
			[FactionTeam.AXIS]   = Shuffled(AllPlayableFactions.Where(f =>  AxisSet.Contains(f) && !_assignments.ContainsKey(f)), rng),
			[FactionTeam.ALLIES] = Shuffled(AllPlayableFactions.Where(f => !AxisSet.Contains(f) && !_assignments.ContainsKey(f)), rng),
		};

		List<int>         pickers      = Shuffled(_randomClaims, rng);
		List<FactionTeam> teams        = pool.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).ToList();
		var               pickersOnTeam = teams.ToDictionary(t => t, _ => new List<int>());

		// Every team with factions left needs someone to take them, so seed one picker each before
		// anything else; the rest go wherever the most factions are waiting per picker, which is what
		// keeps a 6-player lobby dealing one faction apiece instead of three to one player.
		int next = 0;
		foreach (FactionTeam team in teams)
		{
			if (next >= pickers.Count) break; // unreachable while CanCoverAllFactions holds
			pickersOnTeam[team].Add(pickers[next++]);
		}

		for (; next < pickers.Count; next++)
		{
			FactionTeam? target = teams
				.Where(t => pickersOnTeam[t].Count < pool[t].Count)
				.OrderByDescending(t => (double)pool[t].Count / pickersOnTeam[t].Count)
				.Cast<FactionTeam?>()
				.FirstOrDefault();
			if (target == null) break; // unreachable while CanCoverAllFactions holds
			pickersOnTeam[target.Value].Add(pickers[next]);
		}

		foreach (FactionTeam team in teams)
		{
			List<Faction> factions    = pool[team];
			List<int>     teamPickers = pickersOnTeam[team];
			if (teamPickers.Count == 0) continue; // as above: nobody to deal this team to
			for (int i = 0; i < factions.Count; i++)
				_assignments[factions[i]] = teamPickers[i % teamPickers.Count];
		}

		DebugUtilities.PrintPeer($"Random picks dealt: {string.Join(", ", pickers.Select(p => $"peer {p} → {string.Join('/', _assignments.Where(kv => kv.Value == p).Select(kv => FactionNames[kv.Key]))}"))}");
		_randomClaims.Clear();
	}

	private static List<T> Shuffled<T>(IEnumerable<T> source, Random rng)
	{
		var list = source.ToList();
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = rng.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
		return list;
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Game start
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// The game is committed: stop the session accepting anyone else, and stop this node broadcasting.
	///
	/// There is no join-in-progress in this game, so a peer that connects after the start has nothing
	/// to connect to — the host is already in Game.tscn, so the newcomer's join handshake is addressed
	/// to a lobby node that no longer exists there (Godot logs it as "Node not found:
	/// GameRoot/MultiplayerLobby") and the newcomer is left sitting in an empty lobby forever. Easy to
	/// hit with the multi-instance launchers, where a straggler instance can still be booting when the
	/// host clicks Start Game. Refusing the connection outright turns that into a plain "connection
	/// failed" on the newcomer, which is both honest and the behaviour the join screen already handles.
	///
	/// The peer is dropped wholesale when the session ends (SceneFlow.ChangeScene with leaveSession, or
	/// a load auto-hosting afresh), so nothing has to undo this.
	/// </summary>
	private void CloseLobbyToNewPeers()
	{
		_gameStarting = true;
		if (Multiplayer.IsServer() && Multiplayer.MultiplayerPeer != null)
			Multiplayer.MultiplayerPeer.RefuseNewConnections = true;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void StartGame()
	{
		DebugUtilities.PrintPeer($"StartGame: peer {Multiplayer.GetUniqueId()}");
		CloseLobbyToNewPeers();

		var byPeer = new Dictionary<int, List<Faction>>();
		foreach (var (faction, peerId) in _assignments)
		{
			if (!byPeer.ContainsKey(peerId)) byPeer[peerId] = new List<Faction>();
			byPeer[peerId].Add(faction);
		}

		// The name rides along so the game can print "Germany (Bob)" instead of just "Germany". Runs on
		// every peer, but only the host's list is ever serialised onto the wire (StartMultiplayerSession
		// returns early on a client), so a client's mostly-empty name dictionary is harmless here.
		var playerFactionAssignments = byPeer
			.Select(kv => new PlayerFactionAssignment(kv.Key, kv.Value, _playerNames.GetValueOrDefault(kv.Key)))
			.ToList();

		GetNode<GameManager>("/root/GameManager").SetPendingPlayerFactionAssignments(playerFactionAssignments);
		SceneFlow.ChangeScene(this, SceneFlow.GameScenePath);
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Multiplayer signal callbacks
	// ══════════════════════════════════════════════════════════════════════════

	private void OnPeerConnected(long peerId)
	{
		// Should not happen once the peer refuses connections, but a connection already in flight at
		// that moment can still land here, and adding a row to a lobby that is being replaced only
		// produces broadcasts nobody can receive.
		if (_gameStarting) return;

		DebugUtilities.PrintPeer($"Peer connected: {peerId}");
		int    number     = _playerLabels.Count + 1;
		string playerName = $"Player {number}";
		AddPlayerRow((int)peerId, playerName);

		if (_isHost)
		{
			RpcId((int)peerId, nameof(SyncPlayerList),   GetPlayerListData());
			RpcId((int)peerId, nameof(SyncFactionState), SerialiseAssignments(), SerialiseRandomClaims());
			// Not while restoring: SyncScenarioSelection sends a PATH that the client looks up in its own
			// AvailableScenarios, and a save carries its scenario rather than pointing at one — the file may
			// not be there at all. The banner tells them what is being restored instead. Harmless either
			// way: the scenario is read host-side only.
			if (_restoreSave == null)
				RpcId((int)peerId, nameof(SyncScenarioSelection), GetNode<GameManager>("/root/GameManager").SelectedScenario?.Path ?? string.Empty);
			else
				RpcId((int)peerId, nameof(SyncRestoreBanner), RestoreBannerText());

			RpcId((int)peerId, nameof(SyncOpeningDiscard), _openingDiscardCheckBox.ButtonPressed);

			// Cannot match by name yet — the peer has not reported one, so _playerNames holds nothing for
			// it and only the "Player N" placeholder is on the label. Worth doing anyway: passes B and C
			// rebalance immediately, so the new row is never sitting there blank until they report.
			ReseatForRestore();

			if (_dedicatedServer)
			{
				// Dedicated server: assign all factions to the connected clients and start once enough
				// have joined. The server (peer 1) itself controls no faction.
				int connectedClients = _playerLabels.Keys.Count(p => p != 1);
				DebugUtilities.PrintPeer($"Dedicated server: {connectedClients}/{_requiredPlayers} client(s) connected");
				if (connectedClients >= _requiredPlayers)
				{
					AutoAssignDedicatedFactions();
					BroadcastFactionState();
					OnStartGameButtonPressed();
				}
			}
			else if (GameSettings.IsDebugMultiplayer && !_lobbyOnlyMode)
			{
				// F6 debug auto-start: assign default factions, sync to all, then start.
				AutoAssignDebugFactions((int)peerId);
				BroadcastFactionState();
				OnStartGameButtonPressed();
			}
		}
	}

	/// <summary>
	/// Default faction assignment for F6 debug auto-start:
	/// host (peer 1) → Axis, connecting client → Allies.
	/// F7 (swapped_teams=true): host → Allies, client → Axis.
	/// </summary>
	private void AutoAssignDebugFactions(int clientPeerId)
	{
		_assignments.Clear();
		_randomClaims.Clear(); // the auto-split names every faction, so nothing is left to draw
		if (GameSettings.IsDebugTeamsSwapped)
		{
			foreach (var f in AxisSet)                              _assignments[f] = clientPeerId;
			foreach (var f in AllPlayableFactions.Except(AxisSet)) _assignments[f] = 1;
		}
		else
		{
			foreach (var f in AxisSet)                              _assignments[f] = 1;
			foreach (var f in AllPlayableFactions.Except(AxisSet)) _assignments[f] = clientPeerId;
		}
	}

	/// <summary>
	/// Dedicated-server faction assignment: distributes all six factions across the connected
	/// clients (the host/peer 1 is excluded — it controls nothing). With exactly two clients this
	/// mirrors the F6/F7 debug split — first client → Axis, second → Allies (swapped when
	/// swapped_teams=true) — so the same launch produces the same teams as before. With any other
	/// client count, factions are dealt round-robin in join order.
	/// </summary>
	private void AutoAssignDedicatedFactions()
	{
		_assignments.Clear();
		_randomClaims.Clear(); // as above: every faction is dealt here, so no Random claim survives
		List<int> clientPeers = _playerLabels.Keys.Where(p => p != 1).OrderBy(p => p).ToList();
		if (clientPeers.Count == 0) return;

		if (clientPeers.Count == 2)
		{
			int axisClient   = GameSettings.IsDebugTeamsSwapped ? clientPeers[1] : clientPeers[0];
			int alliesClient = GameSettings.IsDebugTeamsSwapped ? clientPeers[0] : clientPeers[1];
			foreach (Faction f in AxisSet)                              _assignments[f] = axisClient;
			foreach (Faction f in AllPlayableFactions.Except(AxisSet)) _assignments[f] = alliesClient;
		}
		else
		{
			for (int i = 0; i < AllPlayableFactions.Count; i++)
				_assignments[AllPlayableFactions[i]] = clientPeers[i % clientPeers.Count];
		}
	}

	private void OnPeerDisconnected(long peerId)
	{
		DebugUtilities.PrintPeer($"Peer disconnected: {peerId}");
		RemovePlayerRow((int)peerId);

		// Everyone is mid-transition into the game, so a released-factions broadcast would arrive at
		// peers that have already swapped the lobby out — and the assignments have been read off
		// already anyway.
		if (_isHost && !_gameStarting)
		{
			var released = _assignments
				.Where(kv => kv.Value == (int)peerId)
				.Select(kv => kv.Key)
				.ToList();
			foreach (var f in released) _assignments.Remove(f);
			_randomClaims.Remove((int)peerId);

			// Fold their seat back onto the host, so the lobby stays startable when someone drops.
			// Broadcasts on its own when restoring; otherwise the call below does it.
			ReseatForRestore();
			BroadcastFactionState();
		}
	}

	private void OnConnectedToServer()
	{
		DebugUtilities.PrintPeer("Connected to server");
		int me = Multiplayer.GetUniqueId();
		UpdateStatusLabel("Connected – waiting for lobby data...");
		AddPlayerRow(me, $"{LocalDisplayName()} (You)", LocalDisplayName());

		// Same reason as the adopted-peer branch in ReadyInternal: the host cannot work a client's name
		// out for itself, so this peer has to report it or the host is left with the "Player N"
		// placeholder — and with no name to put on the assignment, nobody is named in game. This branch
		// is the JoinAt path (F6/F7 debug multiplayer, auto_join), which used to miss the report
		// entirely. Deferred for the same reason: the node has to exist on the far side first.
		CallDeferred(nameof(RequestLobbyStateFromHost));
	}

	private void OnConnectionFailed()
	{
		DebugUtilities.PrintPeerError("Connection failed");
		UpdateStatusLabel("Connection failed!");
	}

	private void OnServerDisconnected()
	{
		DebugUtilities.PrintPeerError("Server disconnected");
		UpdateStatusLabel("Disconnected from server");
		ClearAllRows();
		_assignments.Clear();
		_randomClaims.Clear();
		_startGameButton.Visible = false;

		// The session is over, so release the Steam lobby too — otherwise the next host attempt
		// inherits a stale one and the peer refuses to host on it.
		if (_isSteamSession)
		{
			SteamworksApi.Instance?.LeaveCurrentLobby();
			_isSteamSession       = false;
			_steamLobbyId         = 0;
			_inviteButton.Visible = false;
		}
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Player row UI
	// ══════════════════════════════════════════════════════════════════════════

	/// <param name="reportedName">
	/// The peer's own name, undecorated, for <see cref="_playerNames"/> — or null when
	/// <paramref name="playerName"/> is a placeholder the host invented rather than a name the peer
	/// reported. See the note on <see cref="_playerNames"/>.
	/// </param>
	private void AddPlayerRow(int peerId, string playerName, string reportedName = null)
	{
		if (_playerLabels.ContainsKey(peerId)) return;

		// ── Row background ────────────────────────────────────────────────
		var panel = new PanelContainer();
		var style = new StyleBoxFlat
		{
			BgColor                 = new Color(0.15f, 0.15f, 0.15f, 0.55f),
			CornerRadiusTopLeft     = 4,
			CornerRadiusTopRight    = 4,
			CornerRadiusBottomLeft  = 4,
			CornerRadiusBottomRight = 4
		};
		panel.AddThemeStyleboxOverride("panel", style);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 10);
		panel.AddChild(hbox);

		// ── Player name ───────────────────────────────────────────────────
		var nameLabel = new Label
		{
			Text              = playerName,
			CustomMinimumSize = new Vector2(160, 0),
			VerticalAlignment = VerticalAlignment.Center
		};
		nameLabel.AddThemeColorOverride("font_color", Colors.White);
		hbox.AddChild(nameLabel);
		hbox.AddChild(new VSeparator());

		// ── Faction flags ─────────────────────────────────────────────────
		var flagsBox = new HBoxContainer();
		flagsBox.AddThemeConstantOverride("separation", 6);
		flagsBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddChild(flagsBox);

		_factionButtons[peerId] = new Dictionary<Faction, TextureButton>();
		bool separatorAdded     = false;

		foreach (var faction in AllPlayableFactions)
		{
			// Visual separator between the Axis block (GER/JAP/ITA) and the Allies block.
			if (!separatorAdded && !AxisSet.Contains(faction))
			{
				flagsBox.AddChild(new VSeparator { CustomMinimumSize = new Vector2(2, FlagHeight) });
				separatorAdded = true;
			}

			var tex = GD.Load<Texture2D>(FlagPaths[faction]);
			var btn = new TextureButton
			{
				TextureNormal        = tex,
				TextureDisabled      = tex,    // colour state is driven entirely by Modulate
				StretchMode          = TextureButton.StretchModeEnum.KeepAspectCentered,
				IgnoreTextureSize    = true,
				CustomMinimumSize    = new Vector2(0, FlagHeight),
				SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill,
				TooltipText          = FactionNames[faction]
			};

			// Capture loop variables for the closure.
			int     capPeerId  = peerId;
			Faction capFaction = faction;
			btn.Pressed += () => OnFactionButtonPressed(capPeerId, capFaction);

			flagsBox.AddChild(btn);
			_factionButtons[peerId][faction] = btn;
		}

		// ── Random ────────────────────────────────────────────────────────
		// Set apart from both team blocks by its own separator: it is not a seventh faction, it is the
		// choice not to choose.
		flagsBox.AddChild(new VSeparator { CustomMinimumSize = new Vector2(2, FlagHeight) });

		var randomTex = GD.Load<Texture2D>(RandomFlagPath);
		var randomBtn = new TextureButton
		{
			TextureNormal        = randomTex,
			TextureDisabled      = randomTex,
			StretchMode          = TextureButton.StretchModeEnum.KeepAspectCentered,
			IgnoreTextureSize    = true,
			CustomMinimumSize    = new Vector2(0, FlagHeight),
			SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill,
			TooltipText          = "Random – take whatever nobody picked, drawn when the game starts"
		};

		int capRandomPeerId = peerId;
		randomBtn.Pressed += () => OnRandomButtonPressed(capRandomPeerId);

		flagsBox.AddChild(randomBtn);
		_randomButtons[peerId] = randomBtn;

		_playerListContainer.AddChild(panel);
		_playerLabels[peerId]    = nameLabel;
		_playerRowPanels[peerId] = panel;
		if (reportedName != null) _playerNames[peerId] = reportedName;

		// Initialise visual states for the new row based on current assignments.
		foreach (var (f, b) in _factionButtons[peerId])
			RefreshButton(peerId, f, b);
		RefreshRandomButton(peerId, randomBtn);

		DebugUtilities.PrintPeer($"Added player row: {playerName} (Peer {peerId})");
	}

	private void RemovePlayerRow(int peerId)
	{
		if (_playerRowPanels.TryGetValue(peerId, out var panel))
		{
			panel.QueueFree();
			_playerRowPanels.Remove(peerId);
		}
		_playerLabels.Remove(peerId);
		_playerNames.Remove(peerId);
		_factionButtons.Remove(peerId);
		_randomButtons.Remove(peerId);
		DebugUtilities.PrintPeer($"Removed player row: Peer {peerId}");
	}

	private void ClearAllRows()
	{
		foreach (var panel in _playerRowPanels.Values) panel.QueueFree();
		_playerRowPanels.Clear();
		_playerLabels.Clear();
		_playerNames.Clear();
		_factionButtons.Clear();
		_randomButtons.Clear();
	}

	private Godot.Collections.Dictionary<int, string> GetPlayerListData()
	{
		var d = new Godot.Collections.Dictionary<int, string>();
		foreach (var kv in _playerLabels) d[kv.Key] = kv.Value.Text;
		return d;
	}

	/// <summary>
	/// Client → host: "send me the current lobby state".
	/// Needed because the host's push in <see cref="OnPeerConnected"/> fires the instant the peer
	/// connects, which on the JoinGameScreen path is before this node exists on the client — Godot
	/// resolves RPC targets by node path and silently drops packets for a missing node.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RequestLobbyState()
	{
		if (!Multiplayer.IsServer() || _gameStarting) return;

		int requester = Multiplayer.GetRemoteSenderId();
		RpcId(requester, nameof(SyncPlayerList),   GetPlayerListData());
		RpcId(requester, nameof(SyncFactionState), SerialiseAssignments(), SerialiseRandomClaims());
		// See OnPeerConnected: a restored game syncs its banner rather than a scenario path.
		if (_restoreSave == null)
			RpcId(requester, nameof(SyncScenarioSelection), GetNode<GameManager>("/root/GameManager").SelectedScenario?.Path ?? string.Empty);
		else
			RpcId(requester, nameof(SyncRestoreBanner), RestoreBannerText());

		RpcId(requester, nameof(SyncOpeningDiscard), _openingDiscardCheckBox.ButtonPressed);
	}

	/// <summary>
	/// Rebuilds the entire player list from host-provided data.
	/// Called on newly connected clients to synchronise the current lobby state.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void SyncPlayerList(Godot.Collections.Dictionary<int, string> playerData)
	{
		ClearAllRows();
		foreach (var kv in playerData) AddPlayerRow(kv.Key, kv.Value);
		RefreshAllButtons();
	}

	private void OnScenarioSelected(long selectedIndex)
	{
		if (!_isHost) return;

		var gameManager = GetNode<GameManager>("/root/GameManager");
		// The item's id, not its position: the picker filters tutorials out, so the two differ.
		int index = MenuScenarioPicker.ScenarioIndexAt(_scenarioPicker, (int)selectedIndex);
		if (index < 0 || index >= gameManager.AvailableScenarios.Count) return;

		gameManager.SetSelectedScenarioByIndex(index);
		UpdateScenarioDescription(gameManager.AvailableScenarios[index].Description);
		Rpc(nameof(SyncScenarioSelection), gameManager.SelectedScenario.Path);

		// A new scenario resets the toggle to that scenario's own answer — SetSelectedScenarioByIndex
		// has just dropped the override that went with the previous one.
		SetOpeningDiscard(gameManager.AvailableScenarios[index].OpeningDiscard);
		Rpc(nameof(SyncOpeningDiscard), _openingDiscardCheckBox.ButtonPressed);
	}

	/// <summary>
	/// Host toggled the opening discard. Only the host's box is ever read (it is committed to
	/// GameManager.PendingOpeningDiscard when the game starts), but the clients are told so the lobby
	/// shows everyone the rules they are about to play under.
	/// </summary>
	private void OnOpeningDiscardToggled(bool pressed)
	{
		if (!_isHost) return;
		Rpc(nameof(SyncOpeningDiscard), pressed);
	}

	/// <summary>Set the box without the change echoing back out as a host toggle.</summary>
	private void SetOpeningDiscard(bool enabled)
	{
		_openingDiscardCheckBox.Toggled -= OnOpeningDiscardToggled;
		_openingDiscardCheckBox.ButtonPressed = enabled;
		_openingDiscardCheckBox.Toggled += OnOpeningDiscardToggled;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void SyncOpeningDiscard(bool enabled) => SetOpeningDiscard(enabled);

	/// <summary>Seed entry follows the scenario picker: host-editable, read-only for everyone else.</summary>
	private void SetSeedFieldEnabled(bool enabled)
	{
		_seedInput.Editable = enabled;
		_randomizeSeedButton.Disabled = !enabled;
	}

	private void UpdateScenarioDescription(string description)
	{
		MenuScenarioPicker.ShowDescription(_scenarioDescriptionLabel, description);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void SyncScenarioSelection(string selectedScenarioPath)
	{
		if (string.IsNullOrEmpty(selectedScenarioPath))
			return;

		var gameManager = GetNode<GameManager>("/root/GameManager");
		int selectedIndex = gameManager.AvailableScenarios.FindIndex(s => s.Path == selectedScenarioPath);
		if (selectedIndex < 0) return;

		gameManager.SetSelectedScenarioByIndex(selectedIndex);
		// By path rather than by assigning the index: this picker filters, so the scenario's index in
		// AvailableScenarios is not its position in the list. A host cannot send a tutorial here (its
		// own picker has none), so a miss means a scenario this client does not have — leave the widget
		// alone rather than pointing it at something arbitrary.
		MenuScenarioPicker.SelectByPath(_scenarioPicker, gameManager.AvailableScenarios, selectedScenarioPath);
		UpdateScenarioDescription(gameManager.SelectedScenario.Description);
	}

	private void UpdateStatusLabel(string msg)
	{
		_statusLabel.Text = msg;
		DebugUtilities.PrintPeer($"Lobby: {msg}");
	}
}
