using Godot;

/// <summary>
/// Greys out the board, hand and prompts while the game is paused for a lost connection, and swallows
/// left clicks on them, so nobody answers a prompt the host will not act on until play resumes.
///
/// Placed in the HUD after everything it covers and before the chat, mini menu and Players panel, which
/// stay usable: Godot hands a click to the last control under it in tree order. Other buttons and the
/// wheel pass through (MouseFilter.Pass, not accepted), so the camera can still be moved.
/// </summary>
public partial class PauseInputBlocker : ColorRect
{
    private static readonly Color Tint = new(0, 0, 0, 0.35f);

    public override void _Ready()
    {
        Color = Tint;
        MouseFilter = MouseFilterEnum.Pass;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        bool paused = ConnectionMonitor.Instance?.IsGamePaused == true;
        if (Visible != paused) Visible = paused;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left }) AcceptEvent();
    }
}
