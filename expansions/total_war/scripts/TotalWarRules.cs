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
        rules.AddTurnStep(TurnStep.AIR, after: TurnStep.PLAY_CARD, AirStep.Run,
            new TurnStepText("Air", "Deploy or marshal an Air Force, or gain air superiority.", Optional: true));
        rules.ReallocateDiscardCost = 3;
        // Draw 12 and discard 5; discarding nothing in the Discard step costs 1 VP.
        rules.OpeningHandSize = 12;
        rules.OpeningDiscardCount = 5;
        rules.DiscardNothingPenaltyVP = 1;
    }
}
