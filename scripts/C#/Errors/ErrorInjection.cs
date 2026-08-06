using Godot;
using System;
using System.Linq;

/// <summary>
/// Debug facility for verifying the error-reporting and recovery paths. Enabled only by an explicit
/// command-line argument, following the same <c>OS.GetCmdlineUserArgs()</c> pattern as
/// <c>GameSettings.IsDebugTeamsSwapped</c>:
///
///   godot -- inject_error=supply_step
///
/// Multiple sites can be armed at once with a comma-separated list. Each site fires ONCE per run, so
/// an injected failure inside a loop does not make the game untestable.
///
/// Sites are checked at the call points listed in <see cref="Site"/>. Adding a new one is a single
/// <c>ErrorInjection.MaybeThrow(ErrorInjection.Site.X)</c> line.
/// </summary>
public static class ErrorInjection
{
    public static class Site
    {
        /// <summary>Inside a card step's logic — verifies tier 1 (recover in place).</summary>
        public const string CardStep = "card_step";

        /// <summary>Start of the supply step — verifies tier 2 (halt, then Continue).</summary>
        public const string SupplyStep = "supply_step";

        /// <summary>Start of a new turn — verifies the END-step off-by-one in the resume path.</summary>
        public const string EndStep = "end_step";

        /// <summary>Client handling an input request — verifies the WasSkipped reply unblocks the host.</summary>
        public const string ClientInput = "client_input";

        /// <summary>Mid-ChangeEvent, after mutation, before broadcast — verifies tier 3 (no Continue).</summary>
        public const string MidMutation = "mid_mutation";

        /// <summary>Client applying a queued ChangeEvent — verifies the queue does not wedge.</summary>
        public const string ChangeEventQueue = "change_event_queue";

        /// <summary>Main menu wiring — verifies the popup works with no multiplayer peer present.</summary>
        public const string MenuReady = "menu_ready";

        /// <summary>
        /// Reports a self-recovered failure without throwing — verifies Soft errors are logged but
        /// do NOT raise a popup. The real source is CardStep, which needs a card play to reach.
        /// </summary>
        public const string SoftRecovered = "soft_recovered";
    }

    private static string[] _armed;
    private static readonly System.Collections.Generic.HashSet<string> _fired = new();

    private static string[] Armed
    {
        get
        {
            if (_armed != null) return _armed;

            string arg = OS.GetCmdlineUserArgs()
                .FirstOrDefault(a => a.StartsWith("inject_error=", StringComparison.OrdinalIgnoreCase));

            _armed = arg == null
                ? Array.Empty<string>()
                : arg.Substring("inject_error=".Length)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries)
                     .Select(s => s.Trim())
                     .ToArray();

            if (_armed.Length > 0)
                GD.PrintErr($"[ErrorInjection] ARMED: {string.Join(", ", _armed)}");

            return _armed;
        }
    }

    public static bool IsArmed => Armed.Length > 0;

    /// <summary>
    /// When <c>auto_continue_errors=true</c> is passed, the reporter presses "Continue" for itself on
    /// recoverable errors. Lets the recovery path be exercised in a headless run, where there is no
    /// popup to click. Test-only — never affects a normal launch.
    /// </summary>
    public static bool AutoContinue
        => OS.GetCmdlineUserArgs().Contains("auto_continue_errors=true");

    /// <summary>
    /// Throw a test exception if <paramref name="site"/> is armed and has not fired yet.
    /// No-op (and allocation-free after the first call) in a normal run.
    /// </summary>
    public static void MaybeThrow(string site, string detail = null)
    {
        if (Armed.Length == 0) return;
        if (!Armed.Contains(site, StringComparer.OrdinalIgnoreCase)) return;
        if (!_fired.Add(site)) return;   // once per run

        string message = detail == null
            ? $"Injected test failure at '{site}'"
            : $"Injected test failure at '{site}' ({detail})";
        GD.PrintErr($"[ErrorInjection] throwing: {message}");
        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Report a self-recovered failure at <paramref name="site"/> without throwing, so the Soft
    /// suppression path can be exercised where a real CardStep failure is not reachable.
    /// </summary>
    public static void MaybeReportRecovered(string site, string detail = null)
    {
        if (Armed.Length == 0) return;
        if (!Armed.Contains(site, StringComparer.OrdinalIgnoreCase)) return;
        if (!_fired.Add(site)) return;

        GD.PrintErr($"[ErrorInjection] reporting recovered failure at '{site}'");
        ErrorReporter.ReportRecovered(
            new InvalidOperationException($"Injected self-recovered failure at '{site}'"),
            detail ?? $"injection:{site}");
    }

    /// <summary>True when <paramref name="site"/> is armed and unfired — for conditional test setup.</summary>
    public static bool WillFire(string site)
        => Armed.Length > 0 && Armed.Contains(site, StringComparer.OrdinalIgnoreCase) && !_fired.Contains(site);
}
