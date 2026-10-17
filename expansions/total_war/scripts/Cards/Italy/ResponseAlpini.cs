using System.Collections.Generic;
using System.Threading.Tasks;

public partial class ResponseAlpini : ResponseCardLogic
{
    /// <summary>The Italian Army being battled. The block window has already named it, so this card picks nothing.</summary>
    public override TargetSet Targets() => BlockTargets();

    private bool InOrNextToItaly(int countryId) =>
        countryId == (int)Country.Italy || CountryState.ForEnum(Country.Italy).ConnectedCountryIds.Contains(countryId);

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsBlockRequest(), this),
        Condition.Build(new Condition.CustomCondition(s =>
            s.BlockTrigger is BattleUnitChangeEvent battle
            && battle.UnitState.Faction == Faction
            && battle.UnitState.IsArmy
            && InOrNextToItaly(battle.CountryId)), this)
    };

    /// <summary>As the base removal-blockers (ResponseKwantungArmy): the Army is safe for the rest of the turn.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        new BlockStep<RemoveUnitChangeEvent>(this, async removeEvent => {
            await new ProtectUnitsChangeEvent(Faction, new List<int> { removeEvent.UnitState.Id }).Apply();
            await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} plays Alpini: the Italian Army is not removed").Apply();
            await Task.Delay(GameSettings.DurationMedium);
            return CardStepResult.Block();
        })
        .WithGuidance("Keep your Army in or adjacent to Italy from being removed")
    };
}
