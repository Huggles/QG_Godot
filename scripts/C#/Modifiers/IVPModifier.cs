/// <summary>
/// Implemented by status cards that contribute victory points during the VP step.
/// A card is active as a VP modifier from the moment it is played (registered with
/// ModifierRegistry) until it is discarded (unregistered).
/// </summary>
public interface IVPModifier : IModifier
{
    VPEntry AddVictoryPoints();
}
