/// <summary>
/// Marker interface for all modifier types. A modifier is considered active from the
/// moment it is registered with ModifierRegistry (typically when its card is played)
/// until it is unregistered (typically when its card is discarded).
/// </summary>
public interface IModifier
{
}
