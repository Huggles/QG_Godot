using Godot;

/// <summary>
/// Base for the small choice dialogs the main menu puts up before navigating.
///
/// The chrome — dim backdrop, centred panel, title — lives in
/// <c>res://scenes/menu/MenuModalShell.tscn</c>, which carries this script on its root. Each dialog is
/// an *inherited* scene of that shell: it overrides the root script with its own subclass and adds its
/// contents under <c>%Box</c>, so the whole layout is editable in Godot.
///
/// These are deliberately standalone rather than reusing the game's <c>ModalStack</c>: that one lives
/// inside <c>user_interface.tscn</c> and only exists once a game has loaded, so it is unavailable on the
/// menu.
///
/// Unlike <see cref="ErrorPopup"/> this is NOT owned by an autoload: a menu modal is always dismissed
/// before the scene changes, so it has no reason to outlive its parent.
/// </summary>
public partial class MenuModal : CanvasLayer
{
	/// <summary>Below <see cref="ErrorPopup"/>'s 128, so a crash still draws on top of a modal.</summary>
	protected const int OverlayLayer = 100;

	/// <summary>
	/// Set the moment a choice is made, and checked first by every handler.
	/// Required, not cosmetic: <see cref="MenuPanelButton"/> emits its own Pressed from _GuiInput,
	/// which still fires while the button is Disabled — Disabled only swaps in the dimmed stylebox.
	/// </summary>
	protected bool Resolved;

	/// <summary>
	/// The shell scene already sets both of these; they are reasserted here so a re-save of the scene
	/// that drops them cannot silently sink a modal behind the menu or freeze it on pause.
	/// </summary>
	public override void _Ready()
	{
		Layer = OverlayLayer;
		// The menu does not pause, but a modal that stops responding if anything ever does would be
		// a trap, and this costs nothing.
		ProcessMode = ProcessModeEnum.Always;
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
