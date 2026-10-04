using Godot;
using System.Threading.Tasks;

/// <summary>
/// "Host Game" step one: collect the Steam lobby size and visibility, or host over plain Godot
/// networking (ENet) instead through the small button at the bottom.
///
/// Layout lives in <c>res://scenes/menu/HostOptionsDialog.tscn</c>, an inherited scene of
/// <see cref="MenuModal"/>'s shell — see that class for how the pair fit together.
/// </summary>
public partial class HostOptionsDialog : MenuModal
{
	public enum HostMode { Cancelled, Godot, Steam }

	public sealed record Result(HostMode Mode, int MaxPlayers, SteamLobbyPrivacy Privacy);

	private static readonly Result CancelledResult = new(HostMode.Cancelled, 0, SteamLobbyPrivacy.FriendsOnly);

	private static readonly PackedScene Scene =
		GD.Load<PackedScene>("res://scenes/menu/HostOptionsDialog.tscn");

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
		HostOptionsDialog dialog = Scene.Instantiate<HostOptionsDialog>();
		parent.AddChild(dialog);
		return dialog._result.Task;
	}

	// Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
	// surfacing rather than a silent console line.
	public override void _Ready()
	{
		base._Ready();
		Guard.Try(ReadyInternal, "HostOptionsDialog._Ready");
	}

	public override void _ExitTree() => _result.TrySetResult(CancelledResult);

	private void ReadyInternal()
	{
		_steamOptions  = GetNode<VBoxContainer>("%SteamOptions");
		_maxPlayers    = GetNode<SpinBox>("%MaxPlayers");
		_privacy       = GetNode<OptionButton>("%Privacy");
		_confirmButton = GetNode<Button>("%ConfirmButton");

		Button ipButton = GetNode<Button>("%IpButton");
		ipButton.Pressed += OnGodotPressed;

		GetNode<Button>("%CancelButton").Pressed += Cancel;
		_confirmButton.Pressed += OnConfirmSteam;

		// The %Privacy entries are authored in the scene, and their *ids* — not their order — are the
		// SteamLobbyPrivacy values OnConfirmSteam reads back. Private is 0 and FriendsOnly is 1, so the
		// ids run 1 then 0 down the list. Getting them the wrong way round would silently invert lobby
		// visibility, hence the assertion rather than trust.
		if (_privacy.GetItemId(0) != (int)SteamLobbyPrivacy.FriendsOnly
			|| _privacy.GetItemId(1) != (int)SteamLobbyPrivacy.Private)
			GD.PushError("HostOptionsDialog: %Privacy item ids do not match SteamLobbyPrivacy.");

		// Steam can be perfectly available while the peer class is missing (that happens with the
		// plain GodotSteam build), so both conditions are reported separately.
		string unavailable = SteamUnavailableReason();
		if (unavailable == null)
		{
			// Steam is the default, so Enter creates the lobby.
			_confirmButton.CallDeferred(Control.MethodName.GrabFocus);
			return;
		}

		// IP is the only way left, so it stops being the understated "instead" option.
		_steamOptions.Visible  = false;
		_confirmButton.Visible = false;
		GetNode<Label>("%Hint").Visible = false;
		ipButton.Text = "Host over IP";
		ipButton.Flat = false;
		ipButton.CustomMinimumSize = _confirmButton.CustomMinimumSize;
		ipButton.CallDeferred(Control.MethodName.GrabFocus);

		Label warning = GetNode<Label>("%WarningLabel");
		warning.Text    = unavailable;
		warning.Visible = true;
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

	private void OnConfirmSteam()
	{
		if (Resolved || SteamUnavailableReason() != null) return;
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
