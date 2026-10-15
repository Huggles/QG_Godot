using System.Collections.Generic;
using System.Linq;

public partial class StatusUSMC : StatusCardLogic
{
    /// <summary>The sea the Navy was just built in, when that is a Pacific sea or next to one; else null.</summary>
    private CountryState BuiltIn(GameSituation situation)
    {
        CountryState sea = TriggerContextAs<DeployUnitChangeEvent>(situation)?.CountryState;
        // Adjacent for the US: across the Latin America strait only while the Allies hold it.
        bool pacific = sea != null && (TotalWarCards.IsPacificSea(sea)
                                       || situation.Board.AdjacentCountryStates(Faction, sea).Any(TotalWarCards.IsPacificSea));
        return pacific ? sea : null;
    }

    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        BuiltIn(situation) is { } sea ? situation.Board.AdjacentBattleTargets(Faction, CountryType.LAND, sea) : new();

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.HasDeployedNavy(Faction).Immediately(), this),
        Condition.Build(new Condition.CustomCondition(s => BattleTargets(s).Count > 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 1)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => BattleTargets(s).Count > 0), this))
            .WithGuidance("Discard the top card of your draw deck to battle a land space adjacent to the Navy just built"),

        new ResultStep(this, Choose.BattleTargetFrom(c => BattleTargets(c.Situation), (target, c) => target.ToAttackChangeEvent(Faction, c.Board)))
            .RequiringPreviousStep()
    };
}
