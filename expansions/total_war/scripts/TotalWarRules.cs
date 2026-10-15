/// <summary>
/// The Total War expansion's entry point, found and registered by ExpansionLoader at boot. Grows a hook
/// at a time as the engine gains things an expansion switches on (see IExpansionRules).
/// </summary>
public sealed class TotalWarRules : IExpansionRules
{
    public string Id => "total_war";

    public void Register(ExpansionRegistrar registrar)
    {
        // No messages of its own: the Air step uses core events and prompts.
    }

    public void Configure(RuleSet rules)
    {
        rules.AddTurnStep(TurnStep.AIR, after: TurnStep.PLAY_CARD, AirStep.Run);
    }
}
