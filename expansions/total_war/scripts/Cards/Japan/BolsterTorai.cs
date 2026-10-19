using System.Collections.Generic;

/// <summary>
/// "Use when you gain air superiority in or adjacent to the Central Pacific. Discard a Response card from
/// your hand to battle the space from which the enemy Air Force was just removed." Air superiority that
/// removes an Air Force is a RemoveUnitChangeEvent with reason AIR_SUPERIORITY, which records the space.
/// </summary>
public partial class BolsterTorai : BolsterCardLogic
{
    private CountryState SpaceCleared(GameSituation situation)
    {
        if (TriggerContextAs<RemoveUnitChangeEvent>(situation) is not { Reason: UnitRemovalReason.AIR_SUPERIORITY } removed
            || removed.TriggeringFaction != Faction) return null;
        CountryState centralPacific = CountryState.ForEnum(Country.CentralPacific);
        return removed.CountryId == centralPacific.Id || centralPacific.ConnectedCountryIds.Contains(removed.CountryId)
            ? CountryState.ForId(removed.CountryId)
            : null;
    }

    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        SpaceCleared(situation) is { } space ? TotalWarCards.BattleTargetsIn(situation.Board, Faction, [space.Country]) : new();

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.CustomCondition(s => BattleTargets(s).Count > 0).InReactionWindow(), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.TypedDiscardStep(this, CardType.RESPONSE, "Discard a Response card to battle the space the enemy Air Force was removed from",
            s => BattleTargets(s).Count > 0),
        TotalWarCards.BattleStep(this, BattleTargets, "Battle the space the enemy Air Force was removed from").RequiringPreviousStep()
    };
}
