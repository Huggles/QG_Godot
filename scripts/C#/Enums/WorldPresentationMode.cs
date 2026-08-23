/// <summary>
/// How the world map draws itself. Normal is the ordinary board; Tactical adds the striped overlay that
/// paints each country in the colors of the factions occupying it.
///
/// Top level rather than nested in CountryScene because EventBus.WorldPresentationViewChanged carries it:
/// a global signal typed on a scene's nested enum would point the dependency the wrong way round.
/// </summary>
public enum WorldPresentationMode
{
    Normal,
    Tactical
}
