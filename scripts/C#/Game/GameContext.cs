using Godot;
using System.Linq;

/// <summary>
/// Global runtime context flags that the whole codebase can branch on without pulling in the
/// scene tree. The dominant use is <see cref="IsHeadless"/>: when true, the process is a dedicated
/// server (or an automated run) with no window, rendering, audio, or UI — so presentation must be
/// routed to no-op sinks (see <c>PresentationServices</c>) and real-time pacing skipped.
///
/// Three orthogonal questions live here, and conflating them is the trap:
///   <see cref="IsHeadless"/>         — "is there any presentation?"   (drives PresentationServices)
///   <see cref="IsDedicatedServer"/>  — "do we control no faction?"    (drives the lobby auto-host)
///   <see cref="HasScriptedInput"/>   — "who answers InputRequests?"   (drives InputServices)
/// A CLI run is headless AND scripted but is NOT a dedicated server: it controls every faction.
/// </summary>
public static class GameContext
{
    private static bool? _isHeadless;
    private static bool? _isCli;
    private static bool? _isDedicatedServer;

    /// <summary>
    /// True when running without a window, rendering, audio, or UI. Detected once (cached) from
    /// either the <c>dedicated_server=true</c> command-line user arg (used by quick_launch_server.ps1),
    /// the <c>cli=true</c> arg, or a headless display server (<c>godot --headless</c>).
    ///
    /// This is the "no presentation" flag and nothing else — do not use it to ask whether this
    /// process controls factions. See <see cref="IsDedicatedServer"/> for that.
    /// </summary>
    public static bool IsHeadless
    {
        get
        {
            _isHeadless ??= OS.GetCmdlineUserArgs().Contains("dedicated_server=true")
                            || IsCli
                            || DisplayServer.GetName() == "headless";
            return _isHeadless.Value;
        }
    }

    /// <summary>
    /// True when this process is driven from a terminal instead of a UI: it hosts a single-process
    /// game controlling every faction, and answers <c>InputRequest</c>s from stdin (or a replay
    /// script) rather than from clicks. Implies <see cref="IsHeadless"/>; excludes
    /// <see cref="IsDedicatedServer"/>.
    /// </summary>
    public static bool IsCli
    {
        get
        {
            _isCli ??= OS.GetCmdlineUserArgs().Contains("cli=true");
            return _isCli.Value;
        }
    }

    /// <summary>
    /// True when this process is the faction-less authoritative server that auto-hosts and waits for
    /// clients. This is what the lobby branches on — a headless CLI run must NOT take that path, or
    /// it would sit waiting for peers that never arrive.
    ///
    /// Deliberately still true for a bare <c>godot --headless</c> with no args, which is exactly what
    /// <see cref="IsHeadless"/> used to mean here: existing launch scripts keep working unchanged.
    /// </summary>
    public static bool IsDedicatedServer
    {
        get
        {
            _isDedicatedServer ??= !IsCli
                                   && (OS.GetCmdlineUserArgs().Contains("dedicated_server=true")
                                       || DisplayServer.GetName() == "headless");
            return _isDedicatedServer.Value;
        }
    }

    /// <summary>
    /// True when <c>InputRequest</c>s are answered by something other than the local UI. Separate
    /// from <see cref="IsCli"/> on purpose: a future "AI plays a seat in a GUI game" mode flips this
    /// without touching the bootstrap or the presentation flags.
    /// </summary>
    public static bool HasScriptedInput => IsCli;
}
