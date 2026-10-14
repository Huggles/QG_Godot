using System.Collections.Concurrent;

/// <summary>
/// A one-sided bot switch read from the command line: <c>all|none|AXIS|ALLIES</c>. One side on and the
/// other off is how a valuer change is measured against its absence. The game can also force a switch
/// for one faction (<see cref="Force"/>): that is how a Hard AI seat plays with switches the sim
/// measured, without a command line.
/// </summary>
public static class BotSides
{
    // The valuer asks on every projected board, so each switch is parsed once.
    private static readonly ConcurrentDictionary<string, string> Parsed = new();

    // Set per faction by the game itself — a Hard AI seat — and consulted before the command line.
    private static readonly ConcurrentDictionary<(string, Faction), bool> Forced = new();

    /// <summary>Switch <paramref name="argName"/> on or off for one faction, whatever the command line says.</summary>
    public static void Force(string argName, Faction faction, bool on) => Forced[(argName, faction)] = on;

    /// <summary>Drop every forced switch for <paramref name="faction"/>, back to the command line.</summary>
    public static void Unforce(Faction faction)
    {
        foreach ((string, Faction) key in Forced.Keys)
            if (key.Item2 == faction) Forced.TryRemove(key, out _);
    }

    /// <summary>Drop every forced switch.</summary>
    public static void ClearForced() => Forced.Clear();

    public static bool Includes(string argName, string defaultWho, Faction valuer)
    {
        if (Forced.TryGetValue((argName, valuer), out bool forced)) return forced;
        string who = Parsed.GetOrAdd(argName, name => CliArgs.Get(name, defaultWho).ToUpperInvariant());
        return who switch
        {
            "ALL" => true,
            "NONE" => false,
            _ => StaticGameData.FactionTeamForFaction(valuer).ToString() == who,
        };
    }
}
