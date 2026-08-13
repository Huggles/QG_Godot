using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Lists the games our Steam friends are hosting and joins one.
///
/// Deliberately shaped like <see cref="JoinGameScreen"/> — it establishes the connection here so the
/// player gets immediate feedback, then hands the live peer to <see cref="MultiplayerLobby"/>, which
/// adopts it rather than creating a second one.
/// </summary>
public partial class SteamFriendLobbiesScreen : Control
{
	private const string LobbyScenePath = "res://scenes/menu/MultiplayerLobby.tscn";
	private const string MenuScenePath  = "res://scenes/menu/Menu.tscn";

	/// <summary>Safety net for a peer that never reports success or failure.</summary>
	private const double ConnectTimeoutSeconds = 15.0;

	private const string ColorInfo  = "#bbbbbb";
	private const string ColorError = "#d98a8a";

	private VBoxContainer   _lobbyList;
	private RichTextLabel   _statusLabel;
	private MenuPanelButton _refreshButton;
	private MenuPanelButton _leaveButton;

	/// <summary>
	/// True while a refresh or a join is in flight. Required rather than relying on Disabled:
	/// <see cref="MenuPanelButton"/> emits its own Pressed from _GuiInput, which still fires when the
	/// button is Disabled — that only swaps in the dimmed stylebox.
	/// </summary>
	private bool _busy;

	/// <summary>Incremented per attempt so a timeout guard from an abandoned join is ignored.</summary>
	private int _attempt;

	/// <summary>Set once a join succeeds, so _ExitTree knows not to tear the Steam lobby down.</summary>
	private bool _handedOff;

	// ══════════════════════════════════════════════════════════════════════════
	// Godot lifecycle
	// ══════════════════════════════════════════════════════════════════════════

	// Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
	// surfacing rather than a silent console line.
	public override void _Ready() => Guard.Try(ReadyInternal, "SteamFriendLobbiesScreen._Ready");

	private void ReadyInternal()
	{
		_lobbyList     = GetNode<VBoxContainer>("%LobbyList");
		_statusLabel   = GetNode<RichTextLabel>("%StatusLabel");
		_refreshButton = GetNode<MenuPanelButton>("%RefreshButton");
		_leaveButton   = GetNode<MenuPanelButton>("%LeaveButton");

		_refreshButton.ButtonText = "Refresh";
		_leaveButton.ButtonText   = "Back";

		_refreshButton.Pressed += OnRefreshPressed;
		_leaveButton.Pressed   += OnLeavePressed;

		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed  += OnConnectionFailed;

		if (!SteamworksApi.IsAvailable)
		{
			SetStatus($"Steam is unavailable: {SteamworksApi.UnavailableReason}.", ColorError);
			return;
		}

		// Arrived by accepting an overlay invite: join straight away rather than making the player
		// hunt for the lobby in a list it may not even appear in (private lobbies never do).
		long invite = MainMenu.ConsumePendingInvite();
		if (invite != 0)
		{
			Guard.FireAndForget(() => JoinAsync(invite, "your friend's game"), "SteamFriendLobbies.Invite");
			return;
		}

		Guard.FireAndForget(RefreshAsync, "SteamFriendLobbies.InitialRefresh");
	}

	public override void _ExitTree()
	{
		Multiplayer.ConnectedToServer -= OnConnectedToServer;
		Multiplayer.ConnectionFailed  -= OnConnectionFailed;
		_attempt++; // a pending timeout guard must not outlive the screen

		// On the success path MultiplayerLobby adopts both the peer and the Steam lobby it rides on,
		// so neither may be torn down here. Any other exit releases the lobby.
		if (!_handedOff)
			SteamworksApi.Instance?.LeaveCurrentLobby();
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Refresh
	// ══════════════════════════════════════════════════════════════════════════

	private void OnRefreshPressed()
	{
		if (_busy) return;
		Guard.FireAndForget(RefreshAsync, "SteamFriendLobbies.Refresh");
	}

	private async Task RefreshAsync()
	{
		if (_busy) return;
		SetBusy(true);
		SetStatus("Looking for friends' games...", ColorInfo);

		try
		{
			List<FriendLobby> lobbies = await SteamworksApi.Instance.GetFriendLobbiesAsync();
			if (!IsInstanceValid(this)) return;

			RebuildRows(lobbies);
			SetStatus(
				lobbies.Count == 0
					? "No friends are hosting a game right now."
					: $"{lobbies.Count} game(s) found.",
				ColorInfo);
		}
		finally
		{
			if (IsInstanceValid(this)) SetBusy(false);
		}
	}

	private void RebuildRows(List<FriendLobby> lobbies)
	{
		foreach (Node child in _lobbyList.GetChildren())
			child.QueueFree();

		foreach (FriendLobby lobby in lobbies)
			_lobbyList.AddChild(BuildRow(lobby));
	}

	/// <summary>One lobby row, styled like <see cref="MultiplayerLobby"/>'s player rows.</summary>
	private PanelContainer BuildRow(FriendLobby lobby)
	{
		var panel = new PanelContainer();
		var style = new StyleBoxFlat
		{
			BgColor            = new Color(0.15f, 0.15f, 0.15f, 0.55f),
			CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
			ContentMarginLeft = 10, ContentMarginRight = 10,
			ContentMarginTop  = 8,  ContentMarginBottom = 8,
		};
		panel.AddThemeStyleboxOverride("panel", style);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		panel.AddChild(row);

		row.AddChild(MakeLabel(lobby.HostName, 22, new Vector2(260, 0)));

		string scenario = string.IsNullOrWhiteSpace(lobby.ScenarioTitle) ? "—" : lobby.ScenarioTitle;
		Label scenarioLabel = MakeLabel(scenario, 20, Vector2.Zero);
		scenarioLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scenarioLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		row.AddChild(scenarioLabel);

		row.AddChild(MakeLabel($"{lobby.Members}/{lobby.MaxMembers}", 20, new Vector2(90, 0)));

		// A plain Button, not MenuPanelButton: Disabled has to genuinely block the click for a full
		// lobby, and MenuPanelButton's own Pressed signal ignores Disabled.
		var join = new Button
		{
			Text = lobby.IsFull ? "Full" : "Join",
			Disabled = lobby.IsFull,
			CustomMinimumSize = new Vector2(120, 44),
		};
		join.Pressed += () => OnJoinPressed(lobby);
		row.AddChild(join);

		return panel;
	}

	private static Label MakeLabel(string text, int fontSize, Vector2 minSize)
	{
		var label = new Label { Text = text, CustomMinimumSize = minSize, VerticalAlignment = VerticalAlignment.Center };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		return label;
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Join
	// ══════════════════════════════════════════════════════════════════════════

	private void OnJoinPressed(FriendLobby lobby)
	{
		if (_busy) return;
		Guard.FireAndForget(() => JoinAsync(lobby.LobbyId, $"{lobby.HostName}'s game"),
			"SteamFriendLobbies.Join");
	}

	/// <summary>
	/// Joining is two steps that must happen in this order: enter the Steam lobby, then connect the
	/// peer to it. The peer refuses to connect to a lobby this client is not already a member of.
	/// </summary>
	private async Task JoinAsync(long lobbyId, string description)
	{
		if (_busy) return;
		SetBusy(true);
		SetStatus($"Joining {description}...", ColorInfo);

		(bool ok, string error) = await SteamworksApi.Instance.JoinLobbyAsync(lobbyId);
		if (!IsInstanceValid(this)) return;

		if (!ok)
		{
			AbortAttempt($"Could not join — {error}");
			return;
		}

		MultiplayerPeer peer = SteamPeerFactory.CreateClient(lobbyId, out string peerError);
		if (peer == null)
		{
			SteamworksApi.Instance.LeaveCurrentLobby();
			AbortAttempt($"Could not connect — {peerError}");
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
		MainMenu.SetJoinSteam(lobbyId);
		SetStatus("Connecting...", ColorInfo);
		StartTimeoutGuard();
	}

	private void OnConnectedToServer()
	{
		DebugUtilities.PrintPeer("SteamFriendLobbiesScreen: connected, entering lobby");
		_handedOff = true;
		SetStatus("Connected — entering lobby...", ColorInfo);
		SceneFlow.ChangeScene(this, LobbyScenePath);
	}

	private void OnConnectionFailed()
	{
		DebugUtilities.PrintPeerError("SteamFriendLobbiesScreen: connection failed");
		SteamworksApi.Instance?.LeaveCurrentLobby();
		AbortAttempt("Connection failed — the host may have closed the game.");
	}

	private void OnLeavePressed()
	{
		DebugUtilities.PrintPeer("SteamFriendLobbiesScreen: leaving");
		// _ExitTree releases the Steam lobby; leaveSession clears the peer.
		SceneFlow.ChangeScene(this, MenuScenePath, leaveSession: true);
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Helpers
	// ══════════════════════════════════════════════════════════════════════════

	private void StartTimeoutGuard()
	{
		int attempt = ++_attempt;
		GetTree().CreateTimer(ConnectTimeoutSeconds).Timeout += () =>
		{
			// The timer belongs to the SceneTree, not to this node, so it keeps ticking after the
			// screen is freed — on the success path we are already in the lobby when it fires.
			if (!IsInstanceValid(this)) return;
			if (attempt != _attempt || !_busy) return; // superseded or already resolved

			DebugUtilities.PrintPeerError("SteamFriendLobbiesScreen: connection attempt timed out");
			Multiplayer.MultiplayerPeer = null;
			SteamworksApi.Instance?.LeaveCurrentLobby();
			AbortAttempt("Connection timed out — the host may no longer be reachable.");
		};
	}

	private void AbortAttempt(string message)
	{
		_attempt++; // invalidate any pending timeout guard
		SetBusy(false);
		SetStatus(message, ColorError);
	}

	private void SetBusy(bool busy)
	{
		_busy = busy;
		_refreshButton.Disabled = busy;
	}

	private void SetStatus(string message, string colorHex)
	{
		_statusLabel.Text = $"[color={colorHex}][font_size=22]{message}[/font_size][/color]";
	}
}
