using System.Collections.Generic;

/// <summary>"Use when you deploy or marshal an Air Force. Discard the top 1 card of your draw deck to battle a land space adjacent to that Air Force."</summary>
public partial class BolsterJu87 : BolsterCardLogic
{
    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        TotalWarCards.AirForceMovedTo(TriggerContextAs<ChangeEvent>(situation), Faction) is { } airForceIn
            ? situation.Board.AdjacentBattleTargets(Faction, CountryType.LAND, airForceIn)
            : new();

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.CustomCondition(s => BattleTargets(s).Count > 0).InReactionWindow(), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeckDiscardStep(this, 1, "Discard the top card of your draw deck to battle a land space next to the Air Force",
            s => BattleTargets(s).Count > 0),
        TotalWarCards.BattleStep(this, BattleTargets, "Battle a land space next to the Air Force").RequiringPreviousStep()
    };
}
