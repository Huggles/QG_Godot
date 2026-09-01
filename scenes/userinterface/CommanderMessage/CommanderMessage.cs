using Godot;

/// <summary>
/// Root of <c>res://scenes/userinterface/CommanderMessage/CommanderMessage.tscn</c>: the commander
/// portrait with a message panel beside it, and the continue button that dismisses it.
///
/// The continue press goes out on <see cref="EventBus"/> rather than a signal of this node's own.
/// What waits on a commander message is whatever queued it, which is not this scene's parent and
/// generally has no reference to the scene at all, so a local signal would need hand-wiring at
/// every call site.
///
/// Showing and hiding is the other way round: it is always someone deliberately putting this
/// commander on screen or taking them off, so it is a call on the node, reached through
/// <see cref="Current"/>. Nothing here shows or hides itself - not even on continue, which only
/// broadcasts and leaves the decision to whoever is listening.
/// </summary>
public partial class CommanderMessage : Control
{
	/// <summary>
	/// The show animation reached its end. Not emitted for a show that was cut short by another
	/// call, because the animation that would have finished no longer exists. Awaitable:
	/// <c>await ToSignal(commander, CommanderMessage.SignalName.ShowFinished)</c>.
	/// </summary>
	[Signal] public delegate void ShowFinishedEventHandler();

	/// <summary>The hide animation reached its end and the commander is off screen. Same caveat as
	/// <see cref="ShowFinishedEventHandler"/> for an interrupted hide.</summary>
	[Signal] public delegate void HideFinishedEventHandler();

	/// <summary>
	/// The commander in the scene tree, there being at most one at a time. Callers must guard with
	/// <see cref="GodotObject.IsInstanceValid(GodotObject)"/>: a static handle to a freed Godot
	/// object throws on access rather than reading as null.
	/// </summary>
	public static CommanderMessage Current { get; private set; }

	private const float ShowSeconds = 0.3f;
	private const float HideSeconds = 0.2f;
	/// <summary>How far below its resting place the panel starts a show, and drops away on a hide.</summary>
	private const float SlidePixels = 24f;

	/// <summary>
	/// Whether the commander is off screen when the scene enters the tree, which is what a modal
	/// something else opens wants. Off, the scene starts on screen at full opacity and the first
	/// <see cref="HideMessage"/> is what takes it away. Either way nothing animates until asked.
	/// </summary>
	[Export] public bool StartHidden { get; set; } = true;

	/// <summary>Whether the last call was a show. Flips at the start of the animation, not its end.</summary>
	public bool IsShown { get; private set; }

	private Button _continueButton;
	private Control _textModal;
	private Vector2 _modalRestPosition;
	private Tween _tween;

	public override void _Ready()
	{
		Current = this;

		_continueButton = GetNode<Button>("%ContinueButton");
		_continueButton.Pressed += OnContinuePressed;

		// Captured once, before anything can slide it: the panel is freely positioned rather than
		// laid out by a container, so this is the only record of where it belongs once a hide has
		// moved it. Growing with its text changes the panel's size, never this.
		_textModal = GetNode<Control>("TextModal");
		_modalRestPosition = _textModal.Position;

		IsShown = !StartHidden;
		if (StartHidden)
		{
			Visible = false;
			Modulate = new Color(Modulate, 0f);
		}
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (Current == this)
		{
			Current = null;
		}
	}

	/// <summary>
	/// Bring the commander on screen, fading in as the panel rises into place, and emit
	/// <see cref="ShowFinishedEventHandler"/> when that lands. Interrupts a hide in progress and
	/// takes over from wherever it had got to, so a show/hide/show never leaves a half-faded panel.
	/// </summary>
	/// <param name="instant">Skip the animation and land on the end state, signal included.</param>
	public void ShowMessage(bool instant = false)
	{
		IsShown = true;
		Visible = true;
		_tween?.Kill();

		if (instant)
		{
			Modulate = new Color(Modulate, 1f);
			_textModal.Position = _modalRestPosition;
			EmitSignal(SignalName.ShowFinished);
			return;
		}

		_textModal.Position = _modalRestPosition + new Vector2(0f, SlidePixels);

		_tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		_tween.TweenProperty(this, "modulate:a", 1f, ShowSeconds);
		_tween.TweenProperty(_textModal, "position", _modalRestPosition, ShowSeconds);
		// Kill() does not emit Finished, which is what keeps a superseded show from reporting that
		// it completed.
		_tween.Finished += () => EmitSignal(SignalName.ShowFinished);
	}

	/// <summary>
	/// Take the commander off screen, fading out as the panel drops away, and emit
	/// <see cref="HideFinishedEventHandler"/> once it is gone. The node is only made invisible at
	/// the end, so the fade is actually seen.
	/// </summary>
	/// <param name="instant">Skip the animation and land on the end state, signal included.</param>
	public void HideMessage(bool instant = false)
	{
		IsShown = false;
		_tween?.Kill();

		if (instant)
		{
			Modulate = new Color(Modulate, 0f);
			_textModal.Position = _modalRestPosition;
			Visible = false;
			EmitSignal(SignalName.HideFinished);
			return;
		}

		_tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		_tween.TweenProperty(this, "modulate:a", 0f, HideSeconds);
		_tween.TweenProperty(_textModal, "position", _modalRestPosition + new Vector2(0f, SlidePixels), HideSeconds);
		_tween.Finished += () =>
		{
			Visible = false;
			// Put the panel back, so the next show starts its slide from a known place rather than
			// from wherever the hide left it.
			_textModal.Position = _modalRestPosition;
			EmitSignal(SignalName.HideFinished);
		};
	}

	private void OnContinuePressed()
	{
		EventBus.Emit(EventBus.SignalName.CommanderMessageContinued);
	}
}
