using System.Collections.Concurrent;

/// <summary>
/// A one-sided bot switch read from the command line: <c>all|none|AXIS|ALLIES</c>. One side on and the
/// other off is how a valuer change is measured against its absence.
/// </summary>
public static class BotSides
{
    // The valuer asks on every projected board, so each switch is parsed once.
    private static readonly ConcurrentDictionary<string, string> Parsed = new();

    public static bool Includes(string argName, string defaultWho, Faction valuer)
    {
        string who = Parsed.GetOrAdd(argName, name => CliArgs.Get(name, defaultWho).ToUpperInvariant());
        return who switch
        {
            "ALL" => true,
            "NONE" => false,
            _ => StaticGameData.FactionTeamForFaction(valuer).ToString() == who,
        };
    }
}
