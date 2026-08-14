using Godot;
using System.Threading.Tasks;

/// <summary>
/// "Join Game" step one: connect by IP and port as before, or pick from the games our Steam friends
/// are currently hosting.
///
/// Layout lives in <c>res://scenes/menu/JoinOptionsDialog.tscn</c>, an inherited scene of
/// <see cref="MenuModal"/>'s shell.
/// </summary>
public partial class JoinOptionsDialog : MenuModal
{
	public enum JoinMode { Cancelled, DirectIp, SteamFriends }

	private static readonly PackedScene Scene =
		GD.Load<PackedScene>("res://scenes/menu/JoinOptionsDialog.tscn");

	private readonly TaskCompletionSource<JoinMode> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

	/// <summary>See <see cref="HostOptionsDialog.PromptAsync"/> — same contract.</summary>
	public static Task<JoinMode> PromptAsync(Node parent)
	{
		JoinOptionsDialog dialog = Scene.Instantiate<JoinOptionsDialog>();
		parent.AddChild(dialog);
		return dialog._result.Task;
	}

	public override void _Ready()
	{
		base._Ready();
		Guard.Try(ReadyInternal, "JoinOptionsDialog._Ready");
	}

	public override void _ExitTree() => _result.TrySetResult(JoinMode.Cancelled);

	private void ReadyInternal()
	{
		GetNode<MenuPanelButton>("%DirectButton").Pressed += () => Resolve(JoinMode.DirectIp);

		MenuPanelButton friendsButton = GetNode<MenuPanelButton>("%FriendsButton");
		friendsButton.Pressed += OnFriendsPressed;

		GetNode<Button>("%CancelButton").Pressed += Cancel;

		string unavailable = SteamUnavailableReason();
		if (unavailable == null) return;

		friendsButton.Disabled = true;
		friendsButton.Modulate = new Color(1, 1, 1, 0.4f);

		Label warning = GetNode<Label>("%WarningLabel");
		warning.Text    = unavailable;
		warning.Visible = true;
	}

	private static string SteamUnavailableReason()
	{
		if (!SteamworksApi.IsAvailable)    return $"Steam is unavailable: {SteamworksApi.UnavailableReason}.";
		if (!SteamPeerFactory.IsSupported) return "This build has no Steam networking support.";
		return null;
	}

	private void OnFriendsPressed()
	{
		if (Resolved || SteamUnavailableReason() != null) return;
		Resolve(JoinMode.SteamFriends);
	}

	protected override void Cancel() => Resolve(JoinMode.Cancelled);

	private void Resolve(JoinMode mode)
	{
		if (Resolved) return;
		Resolved = true;
		_result.TrySetResult(mode);
		QueueFree();
	}
}
