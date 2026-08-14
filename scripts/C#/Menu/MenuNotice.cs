using Godot;
using System.Threading.Tasks;

/// <summary>
/// Three small menu overlays that share the <see cref="MenuModal"/> shell: a busy notice held open
/// across an await, a dismissable message, and a two-button question.
///
/// Creating a Steam lobby involves a round trip that can take a second or two on a cold Steam
/// connection, and a menu button that simply does nothing for that long reads as broken.
///
/// Layout lives in <c>res://scenes/menu/MenuNotice.tscn</c>; the message label and every button are
/// authored hidden and revealed here, since which of the three forms this is depends on the caller.
/// </summary>
public partial class MenuNotice : MenuModal
{
	private static readonly PackedScene Scene =
		GD.Load<PackedScene>("res://scenes/menu/MenuNotice.tscn");

	/// <summary>True only when the player pressed the confirm button; every other exit is false.</summary>
	private readonly TaskCompletionSource<bool> _dismissed = new(TaskCreationOptions.RunContinuationsAsynchronously);

	private string _title       = "";
	private string _message     = "";
	private bool   _showOk;
	private bool   _showConfirm;
	private string _confirmText = "OK";
	private string _cancelText  = "Cancel";

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

	/// <summary>
	/// Opens a two-button question and completes with the player's answer.
	///
	/// Only the confirm button answers true. The cancel button, Escape and the scene changing out from
	/// under the dialog all answer false, so a caller acting on a "yes" can never be acting on a dialog
	/// that was really dismissed.
	/// </summary>
	public static Task<bool> ShowConfirmAsync(
		Node parent, string title, string message, string confirmText, string cancelText)
	{
		MenuNotice notice = Scene.Instantiate<MenuNotice>();
		notice._title       = title;
		notice._message     = message;
		notice._showConfirm = true;
		notice._confirmText = confirmText;
		notice._cancelText  = cancelText;
		parent.AddChild(notice);
		return notice._dismissed.Task;
	}

	public override void _Ready()
	{
		base._Ready();
		Guard.Try(ReadyInternal, "MenuNotice._Ready");
	}

	public override void _ExitTree() => _dismissed.TrySetResult(false);

	private void ReadyInternal()
	{
		GetNode<Label>("%Title").Text = _title;

		if (!string.IsNullOrEmpty(_message))
		{
			Label messageLabel = GetNode<Label>("%MessageLabel");
			messageLabel.Text    = _message;
			messageLabel.Visible = true;
		}

		if (_showConfirm)
		{
			GetNode<HBoxContainer>("%ButtonRow").Visible = true;

			Button confirm = GetNode<Button>("%ConfirmButton");
			confirm.Text     = _confirmText;
			confirm.Visible  = true;
			confirm.Pressed += Confirm;

			Button cancel = GetNode<Button>("%CancelButton");
			cancel.Text     = _cancelText;
			cancel.Visible  = true;
			cancel.Pressed += Dismiss;
			return;
		}

		if (!_showOk) return;

		GetNode<HBoxContainer>("%ButtonRow").Visible = true;
		GetNode<Button>("%OkButton").Pressed += Dismiss;
	}

	/// <summary>Closes without confirming. Safe to call more than once, and after the node has gone away.</summary>
	public void Dismiss() => Resolve(false);

	private void Confirm() => Resolve(true);

	private void Resolve(bool confirmed)
	{
		if (Resolved || !IsInstanceValid(this)) return;
		Resolved = true;
		_dismissed.TrySetResult(confirmed);
		QueueFree();
	}

	protected override void Cancel()
	{
		// A busy notice has no button and must not be dismissable by Escape — the work behind it is
		// still running and would finish into a dialog the player thinks they closed.
		if (_showOk || _showConfirm) Dismiss();
	}
}
