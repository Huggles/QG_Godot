/// <summary>
/// Implemented by status cards that change what Reallocate Resources costs their faction. Active from the
/// moment the card is played until it is discarded, like every modifier; read by
/// MutatorReallocateResources.DiscardCostFor.
/// </summary>
public interface IReallocateCostModifier : IModifier
{
    Faction Faction { get; }
    /// <summary>The cost with this card, given the cost so far: the session's base, then any earlier card.</summary>
    int ReallocateCost(int cost);
}
