using System.Collections.Generic;

public partial class ResponseSubmarinesSupportFleet : ResponseCardLogic
{
    /// <summary>What can be battled at sea next to the Navy just built. The trigger stays pinned for both battles.</summary>
    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        TriggerContextAs<DeployUnitChangeEvent>(situation)?.CountryState is { } navyIn
            ? situation.Board.AdjacentBattleTargets(Faction, CountryType.SEA, navyIn)
            : new();

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.HasDeployedNavy(Faction).Immediately(), this)
    };

    /// <summary>The second battle is optional: skipping its prompt ends the card.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.BattleStep(this, BattleTargets, "Battle a sea space adjacent to the Navy just built"),
        TotalWarCards.BattleStep(this, BattleTargets, "Battle a second sea space adjacent to the Navy just built")
            .RequiringPreviousStep()
    };
}
