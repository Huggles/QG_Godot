using Godot;
using System.Threading.Tasks;

/// <summary>
/// Two small menu overlays that share the <see cref="MenuModal"/> shell:
/// a busy notice held open across an await, and a dismissable message.
///
/// Creating a Steam lobby involves a round trip that can take a second or two on a cold Steam
/// connection, and a menu button that simply does nothing for that long reads as broken.
/// </summary>
public partial class MenuNotice : MenuModal
{
	private readonly TaskCompletionSource<bool> _dismissed = new(TaskCreationOptions.RunContinuationsAsynchronously);

	private string _title    = "";
	private string _message  = "";
	private bool   _showOk;
	private Label  _messageLabel;

	/// <summary>
	/// Opens a notice with no button. The caller keeps the reference and calls
	/// <see cref="Dismiss"/> when the work finishes.
	/// </summary>
	public static MenuNotice ShowBusy(Node parent, string message)
	{
		MenuNotice notice = new() { _title = message, _showOk = false };
		parent.AddChild(notice);
		return notice;
	}

	/// <summary>Opens a message with an OK button and completes once it is dismissed.</summary>
	public static Task ShowMessageAsync(Node parent, string title, string message)
	{
		MenuNotice notice = new() { _title = title, _message = message, _showOk = true };
		parent.AddChild(notice);
		return notice._dismissed.Task;
	}

	public override void _Ready()
	{
		base._Ready();
		Guard.Try(BuildUi, "MenuNotice._Ready");
	}

	public override void _ExitTree() => _dismissed.TrySetResult(true);

	private void BuildUi()
	{
		VBoxContainer box = BuildShell(_title, minWidth: 620f);

		if (!string.IsNullOrEmpty(_message))
		{
			_messageLabel = MakeLabel(_message, 20, ColorHint);
			_messageLabel.HorizontalAlignment = HorizontalAlignment.Center;
			box.AddChild(_messageLabel);
		}

		if (!_showOk) return;

		HBoxContainer buttons = new() { Alignment = BoxContainer.AlignmentMode.Center };
		box.AddChild(buttons);

		Button ok = new() { Text = "OK", CustomMinimumSize = new Vector2(160, 44) };
		ok.Pressed += Dismiss;
		buttons.AddChild(ok);
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
