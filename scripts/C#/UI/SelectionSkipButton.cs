using Godot;

/// <summary>
/// Skip button shown while a card step is waiting on a board selection
/// (country / unit / battle target). Only the local player's instance is
/// registered as <see cref="Current"/>. Pressing it emits the
/// <c>SelectionSkipped</c> EventBus signal, which the selection handlers race
/// against their click signal to resolve the selection as a skip.
///
/// Extends <see cref="MenuPanelButton"/> so it reuses the standard menu-button
/// look and click behaviour.
/// </summary>
public partial class SelectionSkipButton : MenuPanelButton
{
    public static SelectionSkipButton Current;

    private void OnPressed()
    {
        EventBus.Emit("SelectionSkipped");
    }

    public override void _Ready()
    {
        base._Ready();

        ButtonText = "Skip";

        if (GetMultiplayerAuthority() == Multiplayer.GetUniqueId())
        {
            Current = this;
        }

        Visible = false;
        Pressed += OnPressed;
    }

    public override void _ExitTree()
    {
        Pressed -= OnPressed;
        if (Current == this)
            Current = null;
    }
}
