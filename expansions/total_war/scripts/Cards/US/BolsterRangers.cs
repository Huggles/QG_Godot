using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "Use when you deploy or marshal an Air Force. Discard the top 1 card of your draw deck to eliminate an
/// Army adjacent to that Air Force." An enemy Army: the card's own side never targets its allies.
/// </summary>
public partial class BolsterRangers : BolsterCardLogic
{
    /// <summary>The enemy Armies next to the Air Force just deployed or marshalled, protected ones left out.</summary>
    private List<int> EnemyArmies(GameSituation situation)
    {
        if (TotalWarCards.AirForceMovedTo(TriggerContextAs<ChangeEvent>(situation), Faction) is not { } airForceIn) return new();
        List<int> adjacent = situation.Board.AdjacentCountryIds(Faction, airForceIn);
        return StaticGameData.FactionsForTeam(StaticGameData.OpponentFactionTeamForFaction(Faction))
            .SelectMany(situation.Board.ActiveUnits)
            .Where(unit => unit.IsArmy && adjacent.Contains(situation.Board.CountryOf(unit)) && !situation.Board.ImmuneForTurn(unit))
            .Select(unit => unit.Id).ToList();
    }

    public override TargetSet Targets() => TargetSet.Units(EnemyArmies(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.CustomCondition(s => EnemyArmies(s).Count > 0).InReactionWindow(), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeckDiscardStep(this, 1, "Discard the top card of your draw deck to eliminate an Army next to the Air Force",
            s => EnemyArmies(s).Count > 0),
        new ResultStep(this, Choose.UnitFrom(c => EnemyArmies(c.Situation),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .RequiringPreviousStep()
            .WithGuidance("Eliminate an Army next to the Air Force")
    };
}
