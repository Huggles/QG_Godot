/// <summary>
/// How the world map draws itself. Normal is the ordinary board; the two Tactical views replace it with
/// the striped overlay, painting each country in the colors of whoever is standing there — Tactical by
/// faction, TacticalTeam by side. Both are the same overlay with a different palette, so a country held
/// by Germany and Italy shows two stripes in Tactical and one in TacticalTeam.
///
/// Declaration order is the order the view button cycles through, since BottomLeftMenu walks
/// Enum.GetValues rather than naming the modes.
///
/// Top level rather than nested in CountryScene because EventBus.WorldPresentationViewChanged carries it:
/// a global signal typed on a scene's nested enum would point the dependency the wrong way round.
/// </summary>
public enum WorldPresentationMode
{
    Normal,
    Tactical,
    TacticalTeam
}
