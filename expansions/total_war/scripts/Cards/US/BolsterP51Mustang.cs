using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "Use when you play an Economic Warfare card. The country attacked must discard an additional card for each
/// United States Air Force within 2 spaces of the opponent's Home space." Offered after that card's discard
/// lands, as a discard of its own: the EW card itself is left as it is.
/// </summary>
public partial class BolsterP51Mustang : BolsterCardLogic
{
    /// <summary>The country a US EW card just made discard, or NONE.</summary>
    private Faction Target(GameSituation situation) =>
        TriggerContextIn(situation) is (ForceDiscardCardsChangeEvent or ForceDiscardHandCardsChangeEvent) and var discard
        && discard.TriggeringFaction == Faction
        && discard.SourceCardState?.CardData?.CardType == CardType.ECONOMIC_WARFARE
            ? discard.TargetFaction
            : Faction.NONE;

    /// <summary>The US Air Forces within 2 spaces of the attacked country's Home space.</summary>
    private List<int> AirForcesInRange(GameSituation situation) => Target(situation) is var target && target != Faction.NONE
        ? situation.Board.ActiveAirForceIds(Faction)
            .Where(airForce => PathFindingService.IsWithinGeographicDistance(
                situation.Board.CountryOf(UnitState.ForId(airForce)), situation.Board.HomeSpaceOf(target).Id, 2))
            .ToList()
        : new();

    public override TargetSet Targets() => TargetSet.Units(AirForcesInRange(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.CustomCondition(s => AirForcesInRange(s).Count > 0).InReactionWindow(), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c => new ForceDiscardCardsChangeEvent(Faction, Target(c.Situation), AirForcesInRange(c.Situation).Count)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => AirForcesInRange(s).Count > 0), this))
            .WithGuidance("The country attacked discards a card for each US Air Force within 2 spaces of its Home space")
    };
}
