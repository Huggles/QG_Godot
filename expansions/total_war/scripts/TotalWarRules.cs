/// <summary>
/// The Total War expansion's entry point, found and registered by ExpansionLoader at boot. Grows a hook
/// at a time as the engine gains things an expansion switches on (see IExpansionRules).
/// </summary>
public sealed class TotalWarRules : IExpansionRules
{
    public string Id => "total_war";

    public void Register(ExpansionRegistrar registrar)
    {
        // M0a spike: one message of the expansion's own, proving its types can ride the wire and the save.
        registrar.AddMessage<SpikePingPresentationEventDto>("TW.SpikePing",
            dto => new SpikePingPresentationEvent(dto.TriggeringFaction, dto.Text));
    }
}
