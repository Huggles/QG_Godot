using Godot;
using System.Threading.Tasks;

/// <summary>
/// Two small menu overlays that share the <see cref="MenuModal"/> shell:
/// a busy notice held open across an await, and a dismissable message.
///
/// Creating a Steam lobby involves a round trip that can take a second or two on a cold Steam
/// connection, and a menu button that simply does nothing for that long reads as broken.
///
/// Layout lives in <c>res://scenes/menu/MenuNotice.tscn</c>; the message label and the OK row are
/// authored hidden and revealed here, since which of the two forms this is depends on the caller.
/// </summary>
public partial class MenuNotice : MenuModal
{
	private static readonly PackedScene Scene =
		GD.Load<PackedScene>("res://scenes/menu/MenuNotice.tscn");

	private readonly TaskCompletionSource<bool> _dismissed = new(TaskCreationOptions.RunContinuationsAsynchronously);

	private string _title    = "";
	private string _message  = "";
	private bool   _showOk;

	/// <summary>
	/// Opens a notice with no button. The caller keeps the reference and calls
	/// <see cref="Dismiss"/> when the work finishes.
	/// </summary>
	public static MenuNotice ShowBusy(Node parent, string message)
	{
		MenuNotice notice = Scene.Instantiate<MenuNotice>();
		notice._title  = message;
		notice._showOk = false;
		parent.AddChild(notice);
		return notice;
	}

	/// <summary>Opens a message with an OK button and completes once it is dismissed.</summary>
	public static Task ShowMessageAsync(Node parent, string title, string message)
	{
		MenuNotice notice = Scene.Instantiate<MenuNotice>();
		notice._title   = title;
		notice._message = message;
		notice._showOk  = true;
		parent.AddChild(notice);
		return notice._dismissed.Task;
	}

	public override void _Ready()
	{
		base._Ready();
		Guard.Try(ReadyInternal, "MenuNotice._Ready");
	}

	public override void _ExitTree() => _dismissed.TrySetResult(true);

	private void ReadyInternal()
	{
		GetNode<Label>("%Title").Text = _title;

		if (!string.IsNullOrEmpty(_message))
		{
			Label messageLabel = GetNode<Label>("%MessageLabel");
			messageLabel.Text    = _message;
			messageLabel.Visible = true;
		}

		if (!_showOk) return;

		GetNode<HBoxContainer>("%ButtonRow").Visible = true;
		GetNode<Button>("%OkButton").Pressed += Dismiss;
	}

	/// <summary>Safe to call more than once, and after the node has already gone away.</summary>
	public void Dismiss()
	{
		if (Resolved || !IsInstanceValid(this)) return;
		Resolved = true;
		_dismissed.TrySetResult(true);
		QueueFree();
	}

	protected override void Cancel()
	{
		// A busy notice has no button and must not be dismissable by Escape — the work behind it is
		// still running and would finish into a dialog the player thinks they closed.
		if (_showOk) Dismiss();
	}
}
