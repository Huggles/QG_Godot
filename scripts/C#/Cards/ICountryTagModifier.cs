/// <summary>
/// Implemented by status cards that need to inject or override country tags
/// (e.g. Buildable, Recruitable) after the standard GameStateCalculator pass.
/// Called once per faction recalculation, only for cards currently in the status zone.
/// </summary>
public interface ICountryTagModifier : IModifier
{
    void ApplyTagModifiers(Faction faction);
}
