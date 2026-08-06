using Godot;

/// <summary>
/// Join-by-IP screen, reached from the main menu's "Join Game" button.
/// Establishes the client connection here so the user gets immediate feedback, then hands the
/// live peer over to <see cref="MultiplayerLobby"/>, which adopts it instead of creating a second one.
/// </summary>
public partial class JoinGameScreen : Control
{
	private const string LobbyScenePath = "res://scenes/menu/MultiplayerLobby.tscn";
	private const string MenuScenePath  = "res://scenes/menu/Menu.tscn";

	/// <summary>Safety net for the case where ENet never raises ConnectionFailed, which would
	/// otherwise leave the screen stuck on "Connecting...".</summary>
	private const double ConnectTimeoutSeconds = 10.0;

	private const string ColorInfo  = "#bbbbbb";
	private const string ColorError = "#d98a8a";

	private LineEdit        _ipInput;
	private LineEdit        _portInput;
	private RichTextLabel   _statusLabel;
	private MenuPanelButton _joinButton;
	private MenuPanelButton _leaveButton;

	/// <summary>
	/// True while a connection attempt is in flight. This guard is required, not just cosmetic:
	/// <see cref="MenuPanelButton"/> emits its own Pressed signal from _GuiInput, which still fires
	/// when the button is Disabled — Disabled only swaps in the dimmed stylebox.
	/// </summary>
	private bool _connecting = false;

	/// <summary>Incremented per attempt so a timeout timer from an abandoned attempt is ignored.</summary>
	private int _attempt = 0;

	// ══════════════════════════════════════════════════════════════════════════
	// Godot lifecycle
	// ══════════════════════════════════════════════════════════════════════════

	// Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
	// surfacing rather than a silent console line.
	public override void _Ready() => Guard.Try(ReadyInternal, "JoinGameScreen._Ready");

	private void ReadyInternal()
	{
		_ipInput     = GetNode<LineEdit>("%IpInput");
		_portInput   = GetNode<LineEdit>("%PortInput");
		_statusLabel = GetNode<RichTextLabel>("%StatusLabel");
		_joinButton  = GetNode<MenuPanelButton>("%JoinButton");
		_leaveButton = GetNode<MenuPanelButton>("%LeaveButton");

		_joinButton.ButtonText  = "Join";
		_leaveButton.ButtonText = "Leave";

		_ipInput.Text   = GameSettings.Instance.LastJoinIp;
		_portInput.Text = GameSettings.Instance.LastJoinPort.ToString();

		_joinButton.Pressed  += OnJoinPressed;
		_leaveButton.Pressed += OnLeavePressed;

		// Enter in either field submits.
		_ipInput.TextSubmitted   += _ => OnJoinPressed();
		_portInput.TextSubmitted += _ => OnJoinPressed();

		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed  += OnConnectionFailed;

		SetStatus("Enter the host's address, then press Join.", ColorInfo);
		_ipInput.GrabFocus();
		_ipInput.SelectAll();
	}

	public override void _ExitTree()
	{
		Multiplayer.ConnectedToServer -= OnConnectedToServer;
		Multiplayer.ConnectionFailed  -= OnConnectionFailed;
		// The peer is deliberately left in place: on the success path MultiplayerLobby adopts it.
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Buttons
	// ══════════════════════════════════════════════════════════════════════════

	private void OnJoinPressed()
	{
		if (_connecting) return;

		string ip = _ipInput.Text.Trim();

		// Accept a pasted "host:port" in the address field by splitting the port out.
		int colon = ip.LastIndexOf(':');
		if (colon > 0 && int.TryParse(ip.Substring(colon + 1), out int pastedPort))
		{
			_portInput.Text = pastedPort.ToString();
			ip              = ip.Substring(0, colon).Trim();
			_ipInput.Text   = ip;
		}

		if (string.IsNullOrEmpty(ip))
		{
			SetStatus("Enter a server address.", ColorError);
			_ipInput.GrabFocus();
			return;
		}

		if (!int.TryParse(_portInput.Text.Trim(), out int port) || port < 1 || port > 65535)
		{
			SetStatus("Invalid port — must be a number between 1 and 65535.", ColorError);
			_portInput.GrabFocus();
			return;
		}

		DebugUtilities.PrintPeer($"JoinGameScreen: connecting to {ip}:{port}");

		var peer  = new ENetMultiplayerPeer();
		Error err = peer.CreateClient(ip, port);
		if (err != Error.Ok)
		{
			DebugUtilities.PrintPeerError($"Failed to create client: {err}");
			SetStatus($"Failed to join: {err}", ColorError);
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
		GameSettings.Instance.SetLastJoinAddress(ip, port);

		SetConnecting(true);
		SetStatus($"Connecting to {ip}:{port}...", ColorInfo);
		StartTimeoutGuard();
	}

	/// <summary>
	/// Returns to the main menu. Navigation only — the peer is intentionally left alone, see the
	/// note in <see cref="_ExitTree"/>.
	/// </summary>
	private void OnLeavePressed()
	{
		DebugUtilities.PrintPeer("JoinGameScreen: leaving");
		SceneFlow.ChangeScene(this, MenuScenePath, leaveSession: true);
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Multiplayer signal callbacks
	// ══════════════════════════════════════════════════════════════════════════

	private void OnConnectedToServer()
	{
		DebugUtilities.PrintPeer("JoinGameScreen: connected, entering lobby");
		SetStatus("Connected — entering lobby...", ColorInfo);

		// Deferred: we are inside the multiplayer poll callback, so the scene swap has to wait
		// until the end of the frame (same reason as MainScene and MultiplayerSession).
		SceneFlow.ChangeScene(this, LobbyScenePath);
	}

	private void OnConnectionFailed()
	{
		DebugUtilities.PrintPeerError("JoinGameScreen: connection failed");
		AbortAttempt("Connection failed — check the address and that the host is running.");
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Helpers
	// ══════════════════════════════════════════════════════════════════════════

	private void StartTimeoutGuard()
	{
		int attempt = ++_attempt;
		GetTree().CreateTimer(ConnectTimeoutSeconds).Timeout += () =>
		{
			if (attempt != _attempt || !_connecting) return; // superseded or already resolved
			DebugUtilities.PrintPeerError("JoinGameScreen: connection attempt timed out");
			AbortAttempt("Connection timed out — is the host running and the port reachable?");
		};
	}

	private void AbortAttempt(string message)
	{
		_attempt++; // invalidate any pending timeout guard
		SetConnecting(false);
		SetStatus(message, ColorError);
	}

	private void SetConnecting(bool connecting)
	{
		_connecting          = connecting;
		_joinButton.Disabled = connecting;
		_ipInput.Editable    = !connecting;
		_portInput.Editable  = !connecting;
	}

	private void SetStatus(string message, string colorHex)
	{
		_statusLabel.Text = $"[color={colorHex}][font_size=22]{message}[/font_size][/color]";
	}
}
