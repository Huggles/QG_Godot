using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// "Host Game" step one: pick the expansions the game is played with, collect the Steam lobby size and
/// visibility, or host over plain Godot networking (ENet) instead through the small button at the bottom.
///
/// The expansions are chosen here, before any lobby exists, because they decide who may join: the host
/// turns away anyone who cannot play them (RejoinService.Hello), and the lobby cannot change them.
///
/// Layout lives in <c>res://scenes/menu/HostOptionsDialog.tscn</c>, an inherited scene of
/// <see cref="MenuModal"/>'s shell — see that class for how the pair fit together.
/// </summary>
public partial class HostOptionsDialog : MenuModal
{
	public enum HostMode { Cancelled, Godot, Steam }

	/// <param name="Expansions">The expansions the game is played with, in <see cref="ExpansionIds"/> order.</param>
	public sealed record Result(HostMode Mode, int MaxPlayers, SteamLobbyPrivacy Privacy, List<string> Expansions);

	private static readonly Result CancelledResult = new(HostMode.Cancelled, 0, SteamLobbyPrivacy.FriendsOnly, new List<string>());

	private static readonly PackedScene Scene =
		GD.Load<PackedScene>("res://scenes/menu/HostOptionsDialog.tscn");

	private readonly TaskCompletionSource<Result> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

	private VBoxContainer    _steamOptions;
	private SpinBox          _maxPlayers;
	private OptionButton     _privacy;
	private Button           _confirmButton;
	private ExpansionToggles _expansionToggles;

	/// <summary>A restore's expansions, which the save fixes; null when the player chooses.</summary>
	private List<string> _fixedExpansions;

	/// <summary>
	/// Opens the dialog over <paramref name="parent"/> and completes when the user chooses or cancels.
	/// Always completes — _ExitTree resolves it — so a scene change cannot leave the caller awaiting forever.
	/// <paramref name="fixedExpansions"/> shows a save's expansions, locked, instead of a choice.
	/// </summary>
	public static Task<Result> PromptAsync(Node parent, IEnumerable<string> fixedExpansions = null)
	{
		HostOptionsDialog dialog = Scene.Instantiate<HostOptionsDialog>();
		dialog._fixedExpansions = fixedExpansions == null ? null : ExpansionIds.Normalize(fixedExpansions);
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

		SetUpExpansions();

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

	/// <summary>
	/// One checkbox per installed expansion, ticked by default when the selected scenario asks for it.
	/// Hidden on an install without expansions; locked to the save's when restoring.
	/// </summary>
	private void SetUpExpansions()
	{
		GetNode<Control>("%ExpansionOptions").Visible = ExpansionRegistry.All.Count > 0;
		_expansionToggles = new ExpansionToggles(GetNode<CheckBox>("%ExpansionTemplate"));
		_expansionToggles.Set(_fixedExpansions ?? GameManager.Instance?.SelectedScenario?.Expansions);
		if (_fixedExpansions == null) return;

		_expansionToggles.SetEditable(false);
		GetNode<Label>("%ExpansionHint").Text = "The save was played with these, so they cannot be changed.";
	}

	private List<string> ChosenExpansions => _fixedExpansions ?? _expansionToggles.Checked;

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
	private void OnGodotPressed() => Resolve(new Result(HostMode.Godot, 0, SteamLobbyPrivacy.FriendsOnly, ChosenExpansions));

	private void OnConfirmSteam()
	{
		if (Resolved || SteamUnavailableReason() != null) return;
		Resolve(new Result(
			HostMode.Steam,
			(int)_maxPlayers.Value,
			(SteamLobbyPrivacy)_privacy.GetSelectedId(),
			ChosenExpansions));
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
