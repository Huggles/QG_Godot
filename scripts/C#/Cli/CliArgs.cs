using Godot;
using System.Collections.Generic;

/// <summary>
/// Parsed <c>key=value</c> command-line user args, following the convention every other arg in this
/// project uses (<c>dedicated_server=true</c>, <c>players=2</c>, <c>inject_error=a,b</c>) — i.e.
/// <see cref="OS.GetCmdlineUserArgs"/>, which is everything after the <c>--</c> separator.
///
/// Deliberately NOT <c>DebugUtilities.CommandLineArguments</c>: that one reads
/// <c>OS.GetCmdlineArgs()</c> (engine args, not user args), requires a <c>--</c> prefix on every key,
/// and throws ArgumentException on a duplicate key. It has no callers.
/// </summary>
public static class CliArgs
{
    private static Dictionary<string, string> _args;

    private static Dictionary<string, string> Args
    {
        get
        {
            if (_args != null) return _args;

            _args = new Dictionary<string, string>();
            foreach (string arg in OS.GetCmdlineUserArgs())
            {
                int split = arg.IndexOf('=');
                // Last one wins rather than throwing: repeating a flag on a long command line is a
                // typo we can recover from, not a reason to refuse to boot.
                if (split > 0) _args[arg.Substring(0, split)] = arg.Substring(split + 1);
                else           _args[arg] = "true";
            }
            return _args;
        }
    }

    public static bool Has(string key) => Args.ContainsKey(key);

    public static string Get(string key, string fallback = null)
        => Args.TryGetValue(key, out string value) ? value : fallback;

    public static bool GetBool(string key, bool fallback = false)
        => Args.TryGetValue(key, out string value) ? value == "true" || value == "1" : fallback;

    public static int GetInt(string key, int fallback)
        => Args.TryGetValue(key, out string value) && int.TryParse(value, out int parsed)
            ? parsed : fallback;

    /// <summary>Console logging stays suppressed in CLI mode unless this is set — see DebugUtilities.</summary>
    public static bool Verbose => GetBool("verbose");
}
