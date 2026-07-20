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

	private RichTextLabel _label;
	private string _pendingText = "";

	public override void _Ready()
	{
		_label = GetNode<RichTextLabel>("Label");
		_label.Text = $"[center]{_pendingText}[/center]";
		MouseDefaultCursorShape = CursorShape.PointingHand;
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			EmitSignal(SignalName.Pressed);
			AcceptEvent();
		}
	}
}
