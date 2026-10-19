using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "Use immediately after your last Army on the board is removed. Discard a Build Army card from your hand to
/// eliminate an Axis Army in or adjacent to Moscow." Offered in the after window of the removal that leaves
/// the Soviet Union no Army; its Navies and Air Forces do not count.
/// </summary>
public partial class BolsterBattleOfMoscow : BolsterCardLogic
{
    private bool LastArmyGone(GameSituation situation) =>
        TriggerContextAs<RemoveUnitChangeEvent>(situation) is { } removed
        && removed.UnitState.Faction == Faction && removed.UnitState.IsArmy
        && !situation.Board.ActiveUnits(Faction).Any(unit => unit.IsArmy);

    /// <summary>The Axis Armies in or next to Moscow, protected ones left out.</summary>
    private List<int> AxisArmies(GameSituation situation)
    {
        CountryState moscow = CountryState.ForEnum(Country.Moscow);
        List<int> around = situation.Board.AdjacentCountryIds(Faction, moscow).Append(moscow.Id).ToList();
        return StaticGameData.FactionsForTeam(FactionTeam.AXIS).SelectMany(situation.Board.ActiveUnits)
            .Where(unit => unit.IsArmy && around.Contains(situation.Board.CountryOf(unit)) && !situation.Board.ImmuneForTurn(unit))
            .Select(unit => unit.Id).ToList();
    }

    public override TargetSet Targets() => TargetSet.Units(AxisArmies(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.CustomCondition(s => LastArmyGone(s) && AxisArmies(s).Count > 0).InReactionWindow(), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.TypedDiscardStep(this, CardType.BUILD_ARMY, "Discard a Build Army card to eliminate an Axis Army in or next to Moscow",
            s => AxisArmies(s).Count > 0),
        new ResultStep(this, Choose.UnitFrom(c => AxisArmies(c.Situation),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .RequiringPreviousStep()
            .WithGuidance("Eliminate an Axis Army in or next to Moscow")
    };
}
