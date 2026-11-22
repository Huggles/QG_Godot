using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// "Use at any time. Discard 4 cards from hand to prevent a French Army in Western Europe from being removed
/// this turn." Offered in the removal block window (decided, as Endless Expanses): the Army is protected for
/// the rest of the turn through ProtectUnitsChangeEvent.
/// </summary>
public partial class BolsterMaginotLine : BolsterCardLogic
{
    public override TargetSet Targets() => BlockTargets();

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsBlockRequest(), this),
        Condition.Build(new Condition.UnitAboutToBeRemoved(Faction.FRANCE, UnitType.ARMY).WithCountries([Country.WesternEurope]), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new BlockStep<RemoveUnitChangeEvent>(this, async removeEvent => {
            await new ProtectUnitsChangeEvent(Faction, new List<int> { removeEvent.UnitState.Id }).Apply();
            await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} uses the Maginot Line: the French Army in Western Europe will not be removed this turn").Apply();
            await Task.Delay(GameSettings.DurationMedium);
            return CardStepResult.Block();
        })
        .WithPrice(TotalWarCards.HandDiscardPrice(this, 4))
        .WithGuidance("Discard 4 cards to keep the French Army in Western Europe this turn")
    };
}
