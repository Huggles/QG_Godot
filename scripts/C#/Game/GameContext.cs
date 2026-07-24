using Godot;
using System.Linq;

/// <summary>
/// Global runtime context flags that the whole codebase can branch on without pulling in the
/// scene tree. The dominant use is <see cref="IsHeadless"/>: when true, the process is a dedicated
/// server (or an automated run) with no window, rendering, audio, or UI — so presentation must be
/// routed to no-op sinks (see <c>PresentationServices</c>) and real-time pacing skipped.
/// </summary>
public static class GameContext
{
    private static bool? _isHeadless;

    /// <summary>
    /// True when running as a dedicated/headless server. Detected once (cached) from either the
    /// <c>dedicated_server=true</c> command-line user arg (used by quick_launch_server.ps1) or a
    /// headless display server (<c>godot --headless</c>).
    /// </summary>
    public static bool IsHeadless
    {
        get
        {
            _isHeadless ??= OS.GetCmdlineUserArgs().Contains("dedicated_server=true")
                            || DisplayServer.GetName() == "headless";
            return _isHeadless.Value;
        }
    }
}
