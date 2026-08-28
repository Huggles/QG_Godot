using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class MainMenu : Control
{
	private const string LoadGameScenePath = "res://scenes/menu/LoadGameScreen.tscn";
	private const string LobbyScenePath        = "res://scenes/menu/MultiplayerLobby.tscn";
	private const string JoinScenePath         = "res://scenes/menu/JoinGameScreen.tscn";
	private const string SteamLobbiesScenePath = "res://scenes/menu/SteamFriendLobbiesScreen.tscn";

	public enum LobbyIntent { None, HostGodot, HostSteam, JoinSteam }

	/// <summary>Set before navigating to the lobby so it can auto-connect.</summary>
	public static LobbyIntent PendingLobbyIntent { get; private set; } = LobbyIntent.None;

	/// <summary>The Steam lobby the lobby screen should host on, or has already joined. 0 when unused.</summary>
	public static long PendingSteamLobbyId { get; private set; }

	/// <summary>Player cap chosen in the host dialog; only meaningful for a Steam host.</summary>
	public static int PendingMaxPlayers { get; private set; } = 6;

	/// <summary>
	/// Lobby the player was invited to through the Steam overlay, picked up by the friends screen.
	/// 0 when there is no pending invite.
	/// </summary>
	public static long PendingInviteLobbyId { get; private set; }

	/// <summary>Reads and clears the pending invite, so it can only ever be acted on once.</summary>
	public static long ConsumePendingInvite()
	{
		long lobbyId = PendingInviteLobbyId;
		PendingInviteLobbyId = 0;
		return lobbyId;
	}

	public static void SetHostGodot()
	{
		PendingLobbyIntent  = LobbyIntent.HostGodot;
		PendingSteamLobbyId = 0;
	}

	public static void SetHostSteam(long lobbyId, int maxPlayers)
	{
		PendingLobbyIntent  = LobbyIntent.HostSteam;
		PendingSteamLobbyId = lobbyId;
		PendingMaxPlayers   = maxPlayers;
	}

	public static void SetJoinSteam(long lobbyId)
	{
		PendingLobbyIntent  = LobbyIntent.JoinSteam;
		PendingSteamLobbyId = lobbyId;
	}

	public static void ClearLobbyIntent()
	{
		PendingLobbyIntent  = LobbyIntent.None;
		PendingSteamLobbyId = 0;
	}

	/// <summary>
	/// Guards the async host/join flows. Needed because <see cref="MenuPanelButton"/> emits Pressed
	/// even while Disabled, and because a second click during the await would open a second dialog.
	/// </summary>
	private bool _flowBusy;

	/// <summary>
	/// True while an invite prompt is on screen, so a friend spamming the invite button cannot stack a
	/// pile of dialogs on top of each other.
	/// </summary>
	private bool _invitePromptOpen;

	// Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
	// surfacing rather than a silent console line.
	public override void _Ready() => Guard.Try(ReadyInternal, "MainMenu._Ready");

	private void ReadyInternal()
	{
		ErrorInjection.MaybeThrow(ErrorInjection.Site.MenuReady);

		// You are on the main menu, so nothing is pending. Not folded into ClearLobbyIntent, which
		// MultiplayerLobby.ReadyInternal calls on itself before hosting — clearing the save there would
		// destroy the very payload the lobby had just arrived to restore.
		GameManager.PendingSave = null;
		GameManager.PendingScenarioJson = null;

		var singlePlayer     = GetNode<MenuPanelButton>("%SinglePlayerButton");
		var loadGame         = GetNode<MenuPanelButton>("%LoadGameButton");
		var multiplayerHost  = GetNode<MenuPanelButton>("%MultiplayerHostButton");
		var multiplayerJoin  = GetNode<MenuPanelButton>("%MultiplayerJoinButton");
		var leaderboard      = GetNode<MenuPanelButton>("%LeaderboardButton");
		var settings         = GetNode<MenuPanelButton>("%SettingsButton");
		var quit             = GetNode<MenuPanelButton>("%QuitButton");

		singlePlayer.ButtonText    = "Single Player";
		loadGame.ButtonText        = "Load Game";
		multiplayerHost.ButtonText = "Host Game";
		multiplayerJoin.ButtonText = "Join Game";
		leaderboard.ButtonText     = "Leaderboard";
		settings.ButtonText        = "Settings";
		quit.ButtonText            = "Quit";

		singlePlayer.Pressed    += OnSinglePlayerPressed;
		loadGame.Pressed        += OnLoadGamePressed;
		multiplayerHost.Pressed += OnMultiplayerHostPressed;
		multiplayerJoin.Pressed += OnMultiplayerJoinPressed;
		leaderboard.Pressed     += OnLeaderboardPressed;
		settings.Pressed        += OnSettingsPressed;
		quit.Pressed            += OnQuitPressed;

		// Hidden until Steam confirms the ladder exists — see RevealLeaderboardIfAvailableAsync. Hidden
		// rather than disabled: on a build without Steam there is nothing to explain to the player, and
		// MenuPanelButton fires Pressed even while Disabled anyway.
		leaderboard.Visible = false;
		Guard.FireAndForget(RevealLeaderboardIfAvailableAsync, "MainMenu.LeaderboardProbe");

		if (SteamworksApi.Instance != null)
		{
			SteamworksApi.Instance.JoinRequested  += OnSteamJoinRequested;
			SteamworksApi.Instance.InviteReceived += OnSteamInviteReceived;
		}
	}

	public override void _ExitTree()
	{
		if (SteamworksApi.Instance != null)
		{
			SteamworksApi.Instance.JoinRequested  -= OnSteamJoinRequested;
			SteamworksApi.Instance.InviteReceived -= OnSteamInviteReceived;
		}
	}

	/// <summary>
	/// The player accepted an invite or pressed "Join game" in the Steam overlay. The decision is already
	/// made, so this joins straight away, handing the lobby to the friends screen which owns the
	/// join-then-connect sequence.
	/// </summary>
	private void OnSteamJoinRequested(long lobbyId)
	{
		// An invite can arrive at any time, including mid-session. Joining then would tear down a
		// game in progress, so it is ignored unless we are idle on the menu.
		if (InSession())
		{
			DebugUtilities.PrintPeer($"Ignoring Steam invite to {lobbyId}: already in a session");
			return;
		}

		PendingInviteLobbyId = lobbyId;
		ClearLobbyIntent();
		SceneFlow.ChangeScene(this, SteamLobbiesScenePath);
	}

	/// <summary>
	/// An invite arrived while the game is already open. Steam's own notification is a chat toast that is
	/// easy to miss and drags the player through the overlay, so the invite is put up as a prompt here
	/// instead — but only on the menu, and only when there is nothing else in flight to interrupt.
	/// </summary>
	private void OnSteamInviteReceived(ulong inviterSteamId, long lobbyId)
	{
		if (InSession())
		{
			DebugUtilities.PrintPeer($"Ignoring Steam invite to {lobbyId}: already in a session");
			return;
		}

		// Mid host/join flow the player is already several clicks into something. Interrupting that would
		// also risk accepting while a lobby is halfway through being created, which would strand it.
		if (_flowBusy || _invitePromptOpen)
		{
			DebugUtilities.PrintPeer($"Ignoring Steam invite to {lobbyId}: the menu is busy");
			return;
		}

		_invitePromptOpen = true;
		Guard.FireAndForget(() => PromptInviteAsync(inviterSteamId, lobbyId), "MainMenu.SteamInvite");
	}

	/// <summary>
	/// Whether this process is actually in a multiplayer session.
	///
	/// Deliberately not the obvious <c>MultiplayerPeer != null</c>. Godot's MultiplayerAPI starts life
	/// holding an <see cref="OfflineMultiplayerPeer"/>, which is non-null AND reports its connection
	/// status as Connected, so the null check was already true on a freshly launched main menu — every
	/// incoming Steam invite was being dropped as "already in a session". Same shape as the peer-adoption
	/// check in <see cref="MultiplayerLobby"/>.
	///
	/// Null is still a real state on this path: <see cref="SceneFlow.ChangeScene"/> with leaveSession
	/// assigns null outright, and that does stick.
	/// </summary>
	private bool InSession()
	{
		MultiplayerPeer peer = Multiplayer?.MultiplayerPeer;
		if (peer == null || peer is OfflineMultiplayerPeer) return false;
		return peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected;
	}

	private async Task PromptInviteAsync(ulong inviterSteamId, long lobbyId)
	{
		try
		{
			string inviter = SteamworksApi.Instance.PersonaNameFor(inviterSteamId);

			// A toast, not a dialog: the invite arrived unannounced, so it must not stop the player
			// doing whatever they were already doing on the menu.
			bool join = await MenuToast.ShowAsync(this,
				$"{inviter} invited you to their game.", "Join Game");

			if (!IsInstanceValid(this) || !join) return;

			// Re-checked after the await: the player had all the time in the world to start something
			// else while the prompt was up.
			OnSteamJoinRequested(lobbyId);
		}
		finally
		{
			if (IsInstanceValid(this)) _invitePromptOpen = false;
		}
	}

	private void OnSinglePlayerPressed()
	{
		SceneFlow.ChangeScene(this, "res://scenes/menu/GameModeSelectionScreen.tscn");
	}

	private void OnLoadGamePressed()
	{
		SceneFlow.ChangeScene(this, LoadGameScenePath);
	}

	private void OnMultiplayerHostPressed()
	{
		if (_flowBusy) return;
		_flowBusy = true;
		Guard.FireAndForget(HostFlowAsync, "MainMenu.HostFlow");
	}

	private async Task HostFlowAsync()
	{
		try
		{
			HostOptionsDialog.Result choice = await HostOptionsDialog.PromptAsync(this);
			if (!IsInstanceValid(this)) return;

			switch (choice.Mode)
			{
				case HostOptionsDialog.HostMode.Godot:
					SetHostGodot();
					SceneFlow.ChangeScene(this, LobbyScenePath);
					break;

				case HostOptionsDialog.HostMode.Steam:
					await StartSteamHostAsync(choice);
					break;
			}
		}
		finally
		{
			if (IsInstanceValid(this)) _flowBusy = false;
		}
	}

	/// <summary>
	/// Creates the Steam lobby before navigating. Doing it here rather than in the lobby screen means
	/// a Steam failure leaves the player on the menu with the Godot option still one click away,
	/// instead of stranding them on a lobby screen that never finished hosting.
	/// </summary>
	private async Task StartSteamHostAsync(HostOptionsDialog.Result choice)
	{
		long lobbyId = await HostLaunch.CreateSteamLobbyAsync(
			this, choice, GameManager.Instance?.SelectedScenario?.Title);

		if (!IsInstanceValid(this) || lobbyId == 0) return;   // the helper has already told the player

		SetHostSteam(lobbyId, choice.MaxPlayers);
		SceneFlow.ChangeScene(this, LobbyScenePath);
	}

	private void OnMultiplayerJoinPressed()
	{
		if (_flowBusy) return;
		_flowBusy = true;
		Guard.FireAndForget(JoinFlowAsync, "MainMenu.JoinFlow");
	}

	private async Task JoinFlowAsync()
	{
		try
		{
			JoinOptionsDialog.JoinMode mode = await JoinOptionsDialog.PromptAsync(this);
			if (!IsInstanceValid(this)) return;

			// Both join paths establish the connection on their own screen and hand the live peer to
			// the lobby, which adopts it. No pending intent is needed for the direct-IP path.
			ClearLobbyIntent();

			switch (mode)
			{
				case JoinOptionsDialog.JoinMode.DirectIp:
					SceneFlow.ChangeScene(this, JoinScenePath);
					break;

				case JoinOptionsDialog.JoinMode.SteamFriends:
					SceneFlow.ChangeScene(this, SteamLobbiesScenePath);
					break;
			}
		}
		finally
		{
			if (IsInstanceValid(this)) _flowBusy = false;
		}
	}

	/// <summary>
	/// The same dialog the in-game Escape menu opens — see <see cref="SettingsDialog"/>. Guarded by
	/// <see cref="_flowBusy"/> like the host and join flows: MenuPanelButton fires Pressed even while
	/// Disabled, and a second click during the await would stack a second dialog.
	/// </summary>
	private void OnSettingsPressed()
	{
		if (_flowBusy) return;
		_flowBusy = true;
		Guard.FireAndForget(SettingsFlowAsync, "MainMenu.SettingsFlow");
	}

	private async Task SettingsFlowAsync()
	{
		try
		{
			await SettingsDialog.ShowAsync(this);
		}
		finally
		{
			if (IsInstanceValid(this)) _flowBusy = false;
		}
	}

	/// <summary>
	/// Reveals the Leaderboard button, but only once Steam has confirmed the ladder is actually there.
	///
	/// Without this the button would open a window that can only report failure — no Steam, or an app
	/// whose Steamworks configuration has no <c>Ranked_Base_Game</c> leaderboard on it — so it is simply
	/// not offered in the first place.
	///
	/// Runs in the background rather than blocking <see cref="ReadyInternal"/>: the find is a round trip
	/// to Steam, and a slow answer must not hold up the whole menu.
	/// </summary>
	private async Task RevealLeaderboardIfAvailableAsync()
	{
		if (!SteamworksApi.IsAvailable) return;

		bool exists = await SteamworksApi.Instance.HasLeaderboardAsync(LeaderboardDialog.LeaderboardName);

		// The player had the whole round trip in which to leave the menu.
		if (!IsInstanceValid(this) || !exists) return;

		GetNode<MenuPanelButton>("%LeaderboardButton").Visible = true;
	}

	/// <summary>
	/// Opens the ranked ladder. Guarded by <see cref="_flowBusy"/> like the other dialog flows:
	/// MenuPanelButton fires Pressed even while Disabled, and a second click during the await would
	/// stack a second window.
	/// </summary>
	private void OnLeaderboardPressed()
	{
		if (_flowBusy) return;
		_flowBusy = true;
		Guard.FireAndForget(LeaderboardFlowAsync, "MainMenu.LeaderboardFlow");
	}

	private async Task LeaderboardFlowAsync()
	{
		try
		{
			await LeaderboardDialog.ShowAsync(this);
		}
		finally
		{
			if (IsInstanceValid(this)) _flowBusy = false;
		}
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}

	private static GameResult CreateFakeGameResult()
	{
		// Made-up per-faction, per-round scores over 20 rounds.
		var fakeScores = new Dictionary<Faction, int[]>
		{
			[Faction.GERMANY]        = new[] { 3, 2, 4, 1, 3, 5, 2, 4, 3, 2, 4, 3, 5, 2, 3, 4, 2, 3, 4, 3 },
			[Faction.JAPAN]          = new[] { 2, 3, 1, 4, 2, 3, 1, 2, 3, 2, 1, 3, 2, 4, 2, 1, 3, 2, 3, 2 },
			[Faction.ITALY]          = new[] { 1, 0, 2, 1, 3, 1, 2, 1, 2, 0, 1, 2, 1, 3, 2, 1, 0, 2, 1, 2 },
			[Faction.UNITED_KINGDOM] = new[] { 2, 3, 2, 4, 1, 3, 2, 3, 2, 4, 3, 2, 1, 3, 2, 4, 3, 2, 3, 2 },
			[Faction.SOVIET]         = new[] { 4, 2, 3, 2, 4, 3, 2, 4, 3, 2, 4, 3, 2, 3, 4, 2, 3, 4, 2, 3 },
			[Faction.UNITED_STATES]  = new[] { 1, 2, 3, 2, 3, 4, 2, 3, 2, 3, 4, 2, 3, 2, 3, 4, 2, 3, 2, 3 },
		};

		// Fallback display data for when no game has been loaded (StaticGameData is empty on this path).
		var fakeLabels = new Dictionary<Faction, string>
		{
			[Faction.GERMANY] = "Germany",       [Faction.JAPAN] = "Japan",           [Faction.ITALY] = "Italy",
			[Faction.UNITED_KINGDOM] = "United Kingdom", [Faction.SOVIET] = "Soviet Union", [Faction.UNITED_STATES] = "United States",
		};
		// Match the real faction colours from QGData_Factions_V2.json.
		var fakeColors = new Dictionary<Faction, string>
		{
			[Faction.GERMANY] = "#5f5f5e",        [Faction.JAPAN] = "#ccddee",         [Faction.ITALY] = "#c20070",
			[Faction.UNITED_KINGDOM] = "#b1b103", [Faction.SOVIET] = "#b60101",        [Faction.UNITED_STATES] = "#006d23",
		};

		var factions = new List<FactionResult>();
		int axisTotal = 0;
		int alliesTotal = 0;

		foreach (Faction faction in StaticGameData.PlayableFactions)
		{
			FactionTeam team = StaticGameData.FactionTeamForFaction(faction);
			int[] roundPoints = fakeScores[faction];

			var perRound = new List<RoundScore>();
			int total = 0;
			for (int i = 0; i < roundPoints.Length; i++)
			{
				perRound.Add(new RoundScore { Round = i + 1, Points = roundPoints[i] });
				total += roundPoints[i];
			}

			if (team == FactionTeam.AXIS) axisTotal += total;
			else if (team == FactionTeam.ALLIES) alliesTotal += total;

			factions.Add(new FactionResult
			{
				Faction = faction,
				Team = team,
				Total = total,
				PerRound = perRound,
				FactionData = StaticGameData.FactionDataMap.GetValueOrDefault(faction)
					?? new FactionData
					{
						Index = (int)faction,
						UniqueName = faction.ToString(),
						Label = fakeLabels[faction],
						ColorString = fakeColors[faction]
					}
			});
		}

		return new GameResult
		{
			WinningTeam = alliesTotal > axisTotal ? FactionTeam.ALLIES : FactionTeam.AXIS,
			IsDraw = false,
			AxisTotal = axisTotal,
			AlliesTotal = alliesTotal,
			FinalRound = 20,
			EndReason = "Test result",
			Factions = factions
		};
	}
}
