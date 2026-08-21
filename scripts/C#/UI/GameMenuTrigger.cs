using Godot;

/// <summary>
/// Opens <see cref="GameMenuModal"/> on Escape while a game is running, and arbitrates that key
/// against the modals that already claim it.
///
/// Why the arbitration is not obvious: <see cref="ModalStack"/> hooks Escape through
/// <c>InputManager.KeyClicked</c>, which is emitted on key *release* from <c>_UnhandledInput</c>.
/// This node acts on the *press*, in <c>_UnhandledKeyInput</c>, which Godot runs earlier in the same
/// event's journey. So:
///
/// <list type="bullet">
/// <item>A prompt or info modal is on screen — bail without consuming, and the release still reaches
/// ModalStack, which closes or parks it exactly as before.</item>
/// <item>Nothing on screen — consume the press and open this menu. The release still reaches
/// ModalStack, whose escape target is null, so it does nothing.</item>
/// <item>This menu already open — the <see cref="GameMenuModal.IsOpen"/> check stops a re-open; the
/// modal's own handler closes it.</item>
/// </list>
///
/// No <c>ui_cancel</c> action is introduced: both existing Escape handlers in the project test
/// <c>Key.Escape</c> directly, and adding an action would leave two competing conventions.
/// </summary>
public partial class GameMenuTrigger : Node
{
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (GameContext.IsHeadless) return;

        // user_interface.tscn is instantiated once per player, so without this every local HUD would
        // open its own copy of the menu off one key press.
        if (!IsMultiplayerAuthority()) return;

        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;

        if (GameMenuModal.IsOpen) return;

        // A pending input request owns Escape — see ModalStack.EscapeTarget. Opening this menu over
        // one would also hide the fact that the player still has to answer it.
        if (ModalStack.Current?.HasEscapeTarget == true) return;

        GetViewport().SetInputAsHandled();
        GameMenuModal.Open(GetTree().CurrentScene);
    }
}
