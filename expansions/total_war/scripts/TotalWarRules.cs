/// <summary>
/// The Total War expansion's entry point, found and registered by ExpansionLoader at boot. Grows a hook
/// at a time as the engine gains things an expansion switches on (see IExpansionRules).
/// </summary>
public sealed class TotalWarRules : IExpansionRules
{
    public string Id => "total_war";

    public void Register(ExpansionRegistrar registrar)
    {
        // Nothing to contribute yet: the engine hooks Total War switches on arrive with M1 onwards.
    }
}
