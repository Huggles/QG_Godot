/// <summary>
/// The fixed bonuses and penalties rules add on top of the valuer. Under <c>bot_tiebreak_nudges</c> they are
/// shrunk to <c>bot_nudge_scale</c> so they only split options the projection rates equal.
/// </summary>
public static class BotNudge
{
    public static double Of(double points, Faction faction)
        => BotSides.Includes("bot_tiebreak_nudges", "none", faction) ? points * CliArgs.GetDouble("bot_nudge_scale", 0.01) : points;
}
