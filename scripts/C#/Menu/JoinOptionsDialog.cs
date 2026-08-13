using Godot;
using System.Threading.Tasks;

/// <summary>
/// "Join Game" step one: connect by IP and port as before, or pick from the games our Steam friends
/// are currently hosting.
/// </summary>
public partial class JoinOptionsDialog : MenuModal
{
	public enum JoinMode { Cancelled, DirectIp, SteamFriends }

	private readonly TaskCompletionSource<JoinMode> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

	/// <summary>See <see cref="HostOptionsDialog.PromptAsync"/> — same contract.</summary>
	public static Task<JoinMode> PromptAsync(Node parent)
	{
		JoinOptionsDialog dialog = new();
		parent.AddChild(dialog);
		return dialog._result.Task;
	}

	public override void _Ready()
	{
		base._Ready();
		Guard.Try(BuildUi, "JoinOptionsDialog._Ready");
	}

	public override void _ExitTree() => _result.TrySetResult(JoinMode.Cancelled);

	private void BuildUi()
	{
		VBoxContainer box = BuildShell("Join Game");

		HBoxContainer choices = new();
		choices.AddThemeConstantOverride("separation", 12);
		choices.Alignment = BoxContainer.AlignmentMode.Center;
		box.AddChild(choices);

		MenuPanelButton directButton = MakeMenuButton("Join by IP", new Vector2(330, 130));
		directButton.Pressed += () => Resolve(JoinMode.DirectIp);
		choices.AddChild(directButton);

		MenuPanelButton friendsButton = MakeMenuButton("Friends' Games", new Vector2(330, 130));
		friendsButton.Pressed += OnFriendsPressed;
		choices.AddChild(friendsButton);

		box.AddChild(MakeLabel(
			"Joining by IP needs the host's address. Friends' games are found through Steam.",
			18, ColorHint));

		string unavailable = SteamUnavailableReason();
		if (unavailable != null)
		{
			friendsButton.Disabled = true;
			friendsButton.Modulate = new Color(1, 1, 1, 0.4f);
			box.AddChild(MakeLabel(unavailable, 18, ColorWarning));
		}

		box.AddChild(new HSeparator());

		HBoxContainer buttons = new();
		box.AddChild(buttons);

		Button cancel = new() { Text = "Cancel", CustomMinimumSize = new Vector2(160, 44) };
		cancel.Pressed += Cancel;
		buttons.AddChild(cancel);
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
