/// <summary>
/// Implemented by status cards that decide who holds a strait, whatever stands in its controlling space
/// (Total War: Spanish Troops Seize Gibraltar). Active from the moment the card is played until it is
/// discarded, like every modifier; read by BoardState.StraitController, which faction-aware adjacency,
/// supply, battles and building all go through.
/// </summary>
public interface IStraitControlModifier : IModifier
{
    /// <summary>The team that holds <paramref name="strait"/> while this card is in play, or null for no say.</summary>
    FactionTeam? StraitControl(StraightState strait);
}
