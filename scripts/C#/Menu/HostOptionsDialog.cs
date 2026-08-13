using Godot;
using System.Threading.Tasks;

/// <summary>
/// "Host Game" step one: host over plain Godot networking (ENet, exactly as before) or over Steam,
/// and for Steam collect the lobby size and visibility.
/// </summary>
public partial class HostOptionsDialog : MenuModal
{
	public enum HostMode { Cancelled, Godot, Steam }

	public sealed record Result(HostMode Mode, int MaxPlayers, SteamLobbyPrivacy Privacy);

	private static readonly Result CancelledResult = new(HostMode.Cancelled, 0, SteamLobbyPrivacy.FriendsOnly);

	/// <summary>The lobby needs all six factions claimed before it will start, so six is the natural default.</summary>
	private const int DefaultMaxPlayers = 6;

	private readonly TaskCompletionSource<Result> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

	private VBoxContainer _steamOptions;
	private SpinBox       _maxPlayers;
	private OptionButton  _privacy;
	private Button        _confirmButton;

	/// <summary>
	/// Opens the dialog over <paramref name="parent"/> and completes when the user chooses or cancels.
	/// Always completes — _ExitTree resolves it — so a scene change cannot leave the caller awaiting forever.
	/// </summary>
	public static Task<Result> PromptAsync(Node parent)
	{
		HostOptionsDialog dialog = new();
		parent.AddChild(dialog);
		return dialog._result.Task;
	}

	public override void _Ready()
	{
		base._Ready();
		Guard.Try(BuildUi, "HostOptionsDialog._Ready");
	}

	public override void _ExitTree() => _result.TrySetResult(CancelledResult);

	private void BuildUi()
	{
		VBoxContainer box = BuildShell("Host Game");

		// ── Transport choice ──────────────────────────────────────────────────
		HBoxContainer choices = new();
		choices.AddThemeConstantOverride("separation", 12);
		choices.Alignment = BoxContainer.AlignmentMode.Center;
		box.AddChild(choices);

		MenuPanelButton godotButton = MakeMenuButton("Host via Godot", new Vector2(330, 130));
		godotButton.Pressed += OnGodotPressed;
		choices.AddChild(godotButton);

		MenuPanelButton steamButton = MakeMenuButton("Host via Steam", new Vector2(330, 130));
		steamButton.Pressed += OnSteamPressed;
		choices.AddChild(steamButton);

		box.AddChild(MakeLabel(
			"Godot hosting uses a direct connection over IP. Steam hosting connects through Steam, "
			+ "so no port forwarding is needed.", 18, ColorHint));

		// Steam can be perfectly available while the peer class is missing (that happens with the
		// plain GodotSteam build), so both conditions are reported separately.
		string unavailable = SteamUnavailableReason();
		if (unavailable != null)
		{
			steamButton.Disabled = true;
			steamButton.Modulate = new Color(1, 1, 1, 0.4f);
			box.AddChild(MakeLabel(unavailable, 18, ColorWarning));
		}

		box.AddChild(new HSeparator());

		// ── Steam-only options, revealed once Steam is chosen ─────────────────
		_steamOptions = new VBoxContainer { Visible = false };
		_steamOptions.AddThemeConstantOverride("separation", 10);
		box.AddChild(_steamOptions);

		HBoxContainer playersRow = new();
		playersRow.AddThemeConstantOverride("separation", 12);
		playersRow.AddChild(MakeLabel("Players", 20));
		_maxPlayers = new SpinBox
		{
			MinValue = 2,
			MaxValue = 6,
			Step     = 1,
			Value    = DefaultMaxPlayers,
			CustomMinimumSize = new Vector2(120, 0),
		};
		playersRow.AddChild(_maxPlayers);
		_steamOptions.AddChild(playersRow);

		HBoxContainer privacyRow = new();
		privacyRow.AddThemeConstantOverride("separation", 12);
		privacyRow.AddChild(MakeLabel("Visibility", 20));
		_privacy = new OptionButton { CustomMinimumSize = new Vector2(420, 0) };
		_privacy.AddItem("Friends only", (int)SteamLobbyPrivacy.FriendsOnly);
		_privacy.AddItem("Private (invite only)", (int)SteamLobbyPrivacy.Private);
		_privacy.Selected = 0;
		privacyRow.AddChild(_privacy);
		_steamOptions.AddChild(privacyRow);

		// Worth stating plainly: a private lobby is invisible in the friends list by design, so
		// without this note an invite-only host looks broken to the people trying to join.
		_steamOptions.AddChild(MakeLabel(
			"Friends-only games appear in your friends' game list. Private games do not — "
			+ "invite players from the lobby instead.", 18, ColorHint));

		// ── Button row ────────────────────────────────────────────────────────
		HBoxContainer buttons = new();
		buttons.AddThemeConstantOverride("separation", 10);
		box.AddChild(buttons);

		Button cancel = new() { Text = "Cancel", CustomMinimumSize = new Vector2(160, 44) };
		cancel.Pressed += Cancel;
		buttons.AddChild(cancel);

		buttons.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		_confirmButton = new Button
		{
			Text = "Create Steam Lobby",
			CustomMinimumSize = new Vector2(260, 44),
			Visible = false,
		};
		_confirmButton.Pressed += OnConfirmSteam;
		buttons.AddChild(_confirmButton);
	}

	/// <summary>Null when Steam hosting is possible, otherwise the reason to show the player.</summary>
	private static string SteamUnavailableReason()
	{
		if (!SteamworksApi.IsAvailable)   return $"Steam is unavailable: {SteamworksApi.UnavailableReason}.";
		if (!SteamPeerFactory.IsSupported) return "This build has no Steam networking support.";
		return null;
	}

	// ══════════════════════════════════════════════════════════════════════════
	// Buttons
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>Resolves straight away: the Godot path must stay exactly as many clicks as before.</summary>
	private void OnGodotPressed() => Resolve(new Result(HostMode.Godot, 0, SteamLobbyPrivacy.FriendsOnly));

	private void OnSteamPressed()
	{
		if (Resolved || SteamUnavailableReason() != null) return;
		_steamOptions.Visible  = true;
		_confirmButton.Visible = true;
	}

	private void OnConfirmSteam()
	{
		if (Resolved) return;
		Resolve(new Result(
			HostMode.Steam,
			(int)_maxPlayers.Value,
			(SteamLobbyPrivacy)_privacy.GetSelectedId()));
	}

	protected override void Cancel() => Resolve(CancelledResult);

	private void Resolve(Result result)
	{
		if (Resolved) return;
		Resolved = true;
		_result.TrySetResult(result);
		QueueFree();
	}
}
