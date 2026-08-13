using Godot;

/// <summary>
/// Base for the small choice dialogs the main menu puts up before navigating.
///
/// Built in code rather than as a .tscn for the same reason <see cref="ErrorPopup"/> is: these have
/// to work on the menu, where <c>PresentationModal</c> does not exist (it lives inside
/// <c>user_interface.tscn</c> and only comes up once a game has loaded). The layout deliberately
/// mirrors ErrorPopup's so the two read the same on screen.
///
/// Unlike ErrorPopup this is NOT owned by an autoload: a menu modal is always dismissed before the
/// scene changes, so it has no reason to outlive its parent.
/// </summary>
public partial class MenuModal : CanvasLayer
{
	/// <summary>Below <see cref="ErrorPopup"/>'s 128, so a crash still draws on top of a modal.</summary>
	protected const int OverlayLayer = 100;

	private static readonly PackedScene ButtonScene =
		GD.Load<PackedScene>("res://scenes/menu/MenuPanelButton.tscn");

	protected static readonly Color ColorHint    = new(0.73f, 0.73f, 0.73f);
	protected static readonly Color ColorWarning = new(0.85f, 0.54f, 0.54f);

	/// <summary>
	/// Set the moment a choice is made, and checked first by every handler.
	/// Required, not cosmetic: <see cref="MenuPanelButton"/> emits its own Pressed from _GuiInput,
	/// which still fires while the button is Disabled — Disabled only swaps in the dimmed stylebox.
	/// </summary>
	protected bool Resolved;

	public override void _Ready()
	{
		Layer = OverlayLayer;
		// The menu does not pause, but a modal that stops responding if anything ever does would be
		// a trap, and this costs nothing.
		ProcessMode = ProcessModeEnum.Always;
	}

	/// <summary>
	/// Builds the dim backdrop and centred panel, and returns the VBox that subclasses fill.
	/// </summary>
	protected VBoxContainer BuildShell(string title, float minWidth = 760f)
	{
		Control root = new() { MouseFilter = Control.MouseFilterEnum.Stop };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		ColorRect dim = new() { Color = new Color(0, 0, 0, 0.72f), MouseFilter = Control.MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(dim);

		CenterContainer centre = new() { MouseFilter = Control.MouseFilterEnum.Pass };
		centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(centre);

		PanelContainer panel = new() { CustomMinimumSize = new Vector2(minWidth, 0) };
		centre.AddChild(panel);

		MarginContainer margin = new();
		margin.AddThemeConstantOverride("margin_left", 24);
		margin.AddThemeConstantOverride("margin_right", 24);
		margin.AddThemeConstantOverride("margin_top", 20);
		margin.AddThemeConstantOverride("margin_bottom", 20);
		panel.AddChild(margin);

		VBoxContainer box = new();
		box.AddThemeConstantOverride("separation", 14);
		margin.AddChild(box);

		Label heading = new() { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
		heading.AddThemeFontSizeOverride("font_size", 30);
		box.AddChild(heading);

		return box;
	}

	/// <summary>A menu-styled button, so these dialogs match the buttons behind them.</summary>
	protected static MenuPanelButton MakeMenuButton(string text, Vector2 minSize)
	{
		MenuPanelButton button = ButtonScene.Instantiate<MenuPanelButton>();
		button.ButtonText       = text;
		button.CustomMinimumSize = minSize;
		return button;
	}

	protected static Label MakeLabel(string text, int fontSize, Color? color = null)
	{
		Label label = new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		if (color.HasValue) label.AddThemeColorOverride("font_color", color.Value);
		return label;
	}

	/// <summary>Escape closes the dialog. The backdrop stops mouse input, but not the keyboard.</summary>
	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (Resolved) return;
		if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
		{
			GetViewport().SetInputAsHandled();
			Cancel();
		}
	}

	/// <summary>Resolve as "no choice made". Subclasses complete their own result here.</summary>
	protected virtual void Cancel() { }
}
