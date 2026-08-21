/// <summary>
/// How the game window presents itself. The values are persisted in <c>settings.cfg</c> and are the
/// item ids of the picker in the settings dialog's Video tab, so they must not be renumbered.
/// </summary>
public enum WindowDisplayMode
{
    /// <summary>A normal resizable window at the chosen resolution.</summary>
    Windowed = 0,

    /// <summary>
    /// A borderless window filling the screen at the desktop resolution — what players usually mean
    /// by "windowed fullscreen". Godot calls this plain <c>WindowMode.Fullscreen</c>.
    /// </summary>
    BorderlessFullscreen = 1,

    /// <summary>True exclusive fullscreen, where the display mode itself is taken over.</summary>
    ExclusiveFullscreen = 2,
}
