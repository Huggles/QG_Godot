using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pushes the saved window mode and resolution onto the actual window.
///
/// Deliberately a static helper rather than an autoload: <see cref="GameSettings"/> already owns
/// every persisted value, and this is only the "apply it" half — the same split as
/// <see cref="GameSettings.SetVolumes"/> handing off to <see cref="AudioManager.ApplyVolumes"/>.
///
/// Everything here no-ops when <see cref="GameContext.IsHeadless"/>. The dedicated server and the
/// CLI runner have no window, and the dummy display driver must never be asked to resize one.
/// </summary>
public static class DisplaySettings
{
    /// <summary>
    /// The resolutions offered in the Video tab, largest last.
    ///
    /// 16:9 only, on purpose: the project renders a 1920x1080 viewport with
    /// <c>stretch/mode="canvas_items"</c> and Godot's default <c>keep</c> aspect, so any other ratio
    /// letterboxes rather than showing more of the board.
    /// </summary>
    private static readonly Vector2I[] Ladder =
    {
        new Vector2I(1280, 720),
        new Vector2I(1600, 900),
        new Vector2I(1920, 1080),
        new Vector2I(2560, 1440),
        new Vector2I(3840, 2160),
    };

    /// <summary>
    /// The <see cref="Ladder"/> entries that fit on the screen the window is currently on, plus
    /// <paramref name="current"/> when the saved size is not one of them — a player who is already
    /// at an odd size must still see their own setting in the picker rather than a silently
    /// different one.
    /// </summary>
    public static IReadOnlyList<Vector2I> AvailableResolutions(Vector2I current)
    {
        if (GameContext.IsHeadless) return new[] { current };

        Vector2I screen = DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen());

        List<Vector2I> options = Ladder
            .Where(size => size.X <= screen.X && size.Y <= screen.Y)
            .ToList();

        // A screen smaller than the smallest ladder entry would otherwise leave an empty picker.
        if (options.Count == 0) options.Add(current);

        if (!options.Contains(current))
        {
            options.Add(current);
            options.Sort((a, b) => a.X.CompareTo(b.X));
        }

        return options;
    }

    /// <summary>
    /// Applies <paramref name="mode"/>, and <paramref name="resolution"/> when the mode is one that
    /// has a resolution of its own.
    /// </summary>
    public static void Apply(WindowDisplayMode mode, Vector2I resolution)
    {
        if (GameContext.IsHeadless) return;

        switch (mode)
        {
            case WindowDisplayMode.BorderlessFullscreen:
                // Godot's plain Fullscreen mode *is* the borderless fullscreen window: it keeps the
                // desktop resolution and does not take over the display mode. The borderless flag is
                // implied by it, so it is not set separately here.
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                break;

            case WindowDisplayMode.ExclusiveFullscreen:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;

            default:
                // Order matters: the size is only honoured once the window is actually windowed, so
                // leaving fullscreen has to happen first.
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                DisplayServer.WindowSetSize(resolution);
                CentreOnScreen(resolution);
                break;
        }
    }

    /// <summary>
    /// Re-centres after a resize. Without this, growing the window keeps the top-left corner pinned
    /// and pushes the bottom-right off screen, which on the largest option puts the whole lower half
    /// of the board out of reach.
    /// </summary>
    private static void CentreOnScreen(Vector2I resolution)
    {
        int screenIndex = DisplayServer.WindowGetCurrentScreen();
        Vector2I screenPosition = DisplayServer.ScreenGetPosition(screenIndex);
        Vector2I screenSize = DisplayServer.ScreenGetSize(screenIndex);
        DisplayServer.WindowSetPosition(screenPosition + (screenSize - resolution) / 2);
    }
}
