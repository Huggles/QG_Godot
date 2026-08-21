using Godot;

/// <summary>
/// Button drawn from a scaled nine patch instead of StyleBoxes.
///
/// Why not StyleBoxes: the frame art is authored far larger than the button and has to be scaled down
/// to draw at the right thickness, and a StyleBoxTexture cannot do that - it stretches its texture to
/// the control, so the border gets thicker as the button grows. The background here is a
/// <see cref="ParentSizedNinePatchRect"/>, which draws at its own scale and compensates its size, so
/// the borders keep a constant thickness whatever the button's size is. All five StyleBox slots on
/// this Button are therefore empty, which also means Godot draws *no* state feedback at all - hover,
/// press, disabled and focus have to be produced here, which is what <see cref="ApplyState"/> does.
/// </summary>
public partial class MenuPanelButton : Button
{
	/// <summary>Precedence order, highest first: a disabled button never looks hovered.</summary>
	private enum VisualState
	{
		Normal,
		Focused,
		Hovered,
		Held,
		Disabled,
	}

	[Export]
	public string ButtonText
	{
		get => _pendingText;
		set
		{
			_pendingText = value;
			ApplyLabelText();
		}
	}

	/// <summary>
	/// Per-state frame art. All optional: an unset state falls back to whatever texture the
	/// background node was authored with, leaving <see cref="_tints"/> to carry the feedback.
	/// </summary>
	[ExportGroup("Background states")]
	[Export] public Texture2D HoverTexture { get; set; }
	[Export] public Texture2D HeldTexture { get; set; }
	[Export] public Texture2D DisabledTexture { get; set; }

	[ExportGroup("State tints")]
	[Export] public Color NormalTint { get; set; } = new(1f, 1f, 1f);
	[Export] public Color FocusedTint { get; set; } = new(1.08f, 1.08f, 1.08f);
	[Export] public Color HoveredTint { get; set; } = new(1.18f, 1.18f, 1.18f);
	[Export] public Color HeldTint { get; set; } = new(0.82f, 0.82f, 0.82f);
	[Export] public Color DisabledTint { get; set; } = new(0.55f, 0.55f, 0.55f, 0.75f);

	[ExportGroup("Content")]
	/// <summary>Dims the label while disabled; the frame tint alone reads as "dark", not "dead".</summary>
	[Export] public Color DisabledContentTint { get; set; } = new(1f, 1f, 1f, 0.45f);

	/// <summary>Pixels the content sinks by while held, for the usual pressed-in feel.</summary>
	[Export] public int HeldContentOffset { get; set; } = 4;

	/// <summary>
	/// Typed as Control, not as the concrete label class: asking for the wrong one gives back null
	/// rather than an error, so <see cref="ButtonText"/> would go quietly nowhere the next time the
	/// scene swaps Label for RichTextLabel or back.
	/// </summary>
	private Control _label => GetNodeOrNull<Control>("ContentContainer/Label");
	private string _pendingText = "";

	private ParentSizedNinePatchRect _background;
	private MarginContainer _content;
	private Texture2D _authoredTexture;
	private int _authoredMarginTop;
	private int _authoredMarginBottom;

	/// <summary>
	/// Set from <see cref="_GuiInput"/> rather than read from <see cref="BaseButton.IsPressed"/>:
	/// this button acts on the press and consumes the event, so BaseButton never gets to track the
	/// press itself and IsPressed stays false throughout.
	/// </summary>
	private bool _held;

	private VisualState _appliedState = VisualState.Normal;

	public override void _Ready()
	{
		_content = GetNodeOrNull<MarginContainer>("ContentContainer");
		_background = FindBackground();

		if (_background != null)
		{
			_authoredTexture = _background.Texture;
		}

		if (_content != null)
		{
			_authoredMarginTop = _content.GetThemeConstant("margin_top");
			_authoredMarginBottom = _content.GetThemeConstant("margin_bottom");
		}

		// The export may have been assigned before this node was in the tree, when the label could not
		// be reached yet.
		ApplyLabelText();

		MouseDefaultCursorShape = CursorShape.PointingHand;

		ApplyState(ResolveState(), force: true);
		RaiseMinimumSizeToArt();
	}

	/// <summary>
	/// Polled rather than signal-driven. Hover, focus and press all have signals, but Disabled is a
	/// plain property with none, and callers do set it (see HostOptionsDialog). One recompute per
	/// frame covers every source of change and cannot drift the way five separate handlers can; the
	/// state is only written through when it actually differs.
	/// </summary>
	public override void _Process(double delta)
	{
		ApplyState(ResolveState());
		RaiseMinimumSizeToArt();
	}

	/// <summary>
	/// A nine patch cannot draw thinner than its own patch margins, so a button smaller than that
	/// gets its border drawn outside itself. The floor can only go through CustomMinimumSize:
	/// overriding _GetMinimumSize does nothing on a Button, whose C++ implementation never consults
	/// the script hook (measured - a Button subclass returning 321x123 still reported 8x8).
	///
	/// Raising only, never setting: a caller that wants a bigger button than the art keeps its value,
	/// and one that asks for less than the art can draw is lifted to what is actually drawable.
	/// </summary>
	private void RaiseMinimumSizeToArt()
	{
		if (_background == null)
		{
			return;
		}

		Vector2 art = _background.GetCombinedMinimumSize() * _background.TileScale;

		if (CustomMinimumSize.X < art.X || CustomMinimumSize.Y < art.Y)
		{
			CustomMinimumSize = new Vector2(
				Mathf.Max(CustomMinimumSize.X, art.X),
				Mathf.Max(CustomMinimumSize.Y, art.Y)
			);
		}
	}

	public override void _Notification(int what)
	{
		// Releasing outside the button, or dragging off it, never sends a release here.
		if (what == NotificationMouseExit)
		{
			_held = false;
		}
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click)
		{
			return;
		}

		if (!click.Pressed)
		{
			_held = false;
			return;
		}

		// Disabled has to block the click, not just dim the button: this signal is emitted by hand,
		// so nothing else enforces it.
		if (Disabled)
		{
			AcceptEvent();
			return;
		}

		_held = true;

		// Here rather than at each call site so every use of this button clicks, including the
		// handful inside the game UI that reuse the menu button widget.
		AudioManager.PlaySfxSetting(AudioManager.MenuButtonClickSetting);

		// BaseButton's own signal, not a second one declared here. A [Signal] named Pressed on this
		// class registers as "Pressed", which is a *different* signal from Button's "pressed": every
		// C# `button.Pressed +=` would bind to the shadow, so keyboard activation - which makes
		// BaseButton emit the real one - would sound a click and then do nothing.
		EmitSignalPressed();
		AcceptEvent();
	}

	/// <summary>
	/// Keyboard and controller activation goes through BaseButton, which never reaches
	/// <see cref="_GuiInput"/> - it emits Pressed itself, so only the click needs adding.
	/// </summary>
	public override void _Pressed()
	{
		AudioManager.PlaySfxSetting(AudioManager.MenuButtonClickSetting);
	}

	/// <summary>
	/// A plain Label centres itself through its alignment properties, so the text goes in raw; a
	/// RichTextLabel needs the centring in the markup instead.
	/// </summary>
	private void ApplyLabelText()
	{
		switch (_label)
		{
			case RichTextLabel rich:
				rich.Text = $"[center]{_pendingText}[/center]";
				break;
			case Label label:
				label.Text = _pendingText;
				break;
		}
	}

	private ParentSizedNinePatchRect FindBackground()
	{
		foreach (Node child in GetChildren())
		{
			if (child is ParentSizedNinePatchRect background)
			{
				return background;
			}
		}

		return null;
	}

	private VisualState ResolveState()
	{
		if (Disabled)
		{
			return VisualState.Disabled;
		}

		if (_held)
		{
			return VisualState.Held;
		}

		if (IsHovered())
		{
			return VisualState.Hovered;
		}

		if (HasFocus())
		{
			return VisualState.Focused;
		}

		return VisualState.Normal;
	}

	private void ApplyState(VisualState state, bool force = false)
	{
		if (state == _appliedState && !force)
		{
			return;
		}

		_appliedState = state;

		if (_background != null)
		{
			_background.Texture = TextureFor(state) ?? _authoredTexture;
			_background.SelfModulate = TintFor(state);
		}

		if (_label != null)
		{
			_label.SelfModulate = state == VisualState.Disabled ? DisabledContentTint : Colors.White;
		}

		if (_content != null)
		{
			int sink = state == VisualState.Held ? HeldContentOffset : 0;

			// Through the margins, not the position: the content is anchored to the button's rect, so
			// a position offset would be recomputed away the next time the button is resized.
			_content.AddThemeConstantOverride("margin_top", _authoredMarginTop + sink);
			_content.AddThemeConstantOverride("margin_bottom", Mathf.Max(0, _authoredMarginBottom - sink));
		}
	}

	private Texture2D TextureFor(VisualState state) => state switch
	{
		VisualState.Hovered => HoverTexture,
		VisualState.Held => HeldTexture,
		VisualState.Disabled => DisabledTexture,
		_ => null,
	};

	private Color TintFor(VisualState state) => state switch
	{
		VisualState.Focused => FocusedTint,
		VisualState.Hovered => HoveredTint,
		VisualState.Held => HeldTint,
		VisualState.Disabled => DisabledTint,
		_ => NormalTint,
	};
}
