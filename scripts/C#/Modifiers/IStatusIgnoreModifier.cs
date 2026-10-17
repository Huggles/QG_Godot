/// <summary>
/// Implemented by status cards that make other cards in play count for nothing for a while (Total War:
/// Hobart's Funnies, Axis Status cards during the United Kingdom turn). Read by BoardState.Modifiers,
/// which leaves an ignored card's modifiers out, and CardLogic.CanBeActivated, which refuses it.
/// </summary>
public interface IStatusIgnoreModifier : IModifier
{
    /// <summary>Whether <paramref name="card"/> is ignored on <paramref name="board"/> right now.</summary>
    bool Ignores(BoardState board, CardLogic card);
}
