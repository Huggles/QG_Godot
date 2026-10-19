using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "Use when you deploy or marshal an Air Force to a sea space. Discard a Response card from your hand to
/// battle a space adjacent to that Air Force." Land or sea.
/// </summary>
public partial class BolsterNagumoCommandsTheFirstAirFleet : BolsterCardLogic
{
    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        TotalWarCards.AirForceMovedTo(TriggerContextAs<ChangeEvent>(situation), Faction) is { IsSea: true } airForceIn
            ? situation.Board.AdjacentBattleTargets(Faction, CountryType.LAND, airForceIn)
                .Concat(situation.Board.AdjacentBattleTargets(Faction, CountryType.SEA, airForceIn)).ToList()
            : new();

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.CustomCondition(s => BattleTargets(s).Count > 0).InReactionWindow(), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.TypedDiscardStep(this, CardType.RESPONSE, "Discard a Response card to battle a space next to the Air Force",
            s => BattleTargets(s).Count > 0),
        TotalWarCards.BattleStep(this, BattleTargets, "Battle a space next to the Air Force").RequiringPreviousStep()
    };
}
