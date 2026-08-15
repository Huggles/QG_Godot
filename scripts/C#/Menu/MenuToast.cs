using Godot;
using System.Threading.Tasks;

/// <summary>
/// A small notification strip along the bottom of the screen with one accept button.
///
/// Deliberately NOT a <see cref="MenuModal"/>. Everything in that family dims the screen, swallows mouse
/// input and grabs Escape, which is right for a question the player asked for and wrong for one that
/// arrives unannounced — an incoming game invite must not stop someone from carrying on with the menu.
/// So this is a bare CanvasLayer whose backdrop is a mouse-ignoring Control: only the strip itself takes
/// clicks, and the menu underneath keeps working as if it were not there.
///
/// Sits at layer 99, one below <see cref="MenuModal"/>, so opening a real dialog covers the toast rather
/// than fighting it for the same space.
///
/// Layout lives in <c>res://scenes/menu/MenuToast.tscn</c>.
/// </summary>
public partial class MenuToast : CanvasLayer
{
	private static readonly PackedScene Scene =
		GD.Load<PackedScene>("res://scenes/menu/MenuToast.tscn");

	/// <summary>True only when the accept button was pressed; every other exit is false.</summary>
	private readonly TaskCompletionSource<bool> _answered = new(TaskCreationOptions.RunContinuationsAsynchronously);

	private string _message    = "";
	private string _acceptText = "OK";

	/// <summary>Set the moment the toast is answered, so a second click cannot resolve it twice.</summary>
	private bool _resolved;

	/// <summary>
	/// Shows the toast and completes with the player's answer. Only the accept button answers true —
	/// the Ignore button and the scene changing out from under it both answer false, so a caller acting
	/// on a "yes" can never be acting on a toast that simply went away.
	/// </summary>
	public static Task<bool> ShowAsync(Node parent, string message, string acceptText)
	{
		MenuToast toast = Scene.Instantiate<MenuToast>();
		toast._message    = message;
		toast._acceptText = acceptText;
		parent.AddChild(toast);
		return toast._answered.Task;
	}

	// Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
	// surfacing rather than a silent console line.
	public override void _Ready() => Guard.Try(ReadyInternal, "MenuToast._Ready");

	public override void _ExitTree() => _answered.TrySetResult(false);

	private void ReadyInternal()
	{
		// Reasserted rather than trusted to the scene: a re-save that dropped either one would sink the
		// toast behind the menu, or freeze it if anything ever pauses.
		Layer       = 99;
		ProcessMode = ProcessModeEnum.Always;

		GetNode<Label>("%MessageLabel").Text = _message;

		Button accept = GetNode<Button>("%AcceptButton");
		accept.Text     = _acceptText;
		accept.Pressed += Accept;

		GetNode<Button>("%DismissButton").Pressed += Dismiss;
	}

	/// <summary>Closes without accepting. Safe to call more than once, and after the node has gone away.</summary>
	public void Dismiss() => Resolve(false);

	private void Accept() => Resolve(true);

	private void Resolve(bool accepted)
	{
		if (_resolved || !IsInstanceValid(this)) return;
		_resolved = true;
		_answered.TrySetResult(accepted);
		QueueFree();
	}
}
