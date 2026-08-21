using Godot;

public partial class MenuPanelButton : Button
{
	[Signal]
	public delegate void PressedEventHandler();

	[Export]
	public string ButtonText
	{
		get => _pendingText;
		set
		{
			_pendingText = value;
			if (_label != null)
				_label.Text = $"[center]{value}[/center]";
		}
	}

	private RichTextLabel _label => GetNode<RichTextLabel>("MarginContainer/Label");
	private string _pendingText = "";

	public override void _Ready()
	{		
		_label.Text = $"[center]{_pendingText}[/center]";
		MouseDefaultCursorShape = CursorShape.PointingHand;
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			// Here rather than at each call site so every use of this button clicks, including the
			// handful inside the game UI that reuse the menu button widget.
			AudioManager.PlaySfxSetting(AudioManager.MenuButtonClickSetting);
			EmitSignal(SignalName.Pressed);
			AcceptEvent();
		}
	}
}
