using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// "Use when your Army is battled. Discard a Response card from your hand and do not remove the Army due to
/// that battle." Blocks that battle's removal only: no protection for the rest of the turn.
/// </summary>
public partial class BolsterCodeOfBushido : BolsterCardLogic
{
    public override TargetSet Targets() => BlockTargets();

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsBlockRequest(), this),
        Condition.Build(new Condition.UnitAboutToBeRemoved(Faction, UnitType.ARMY), this),
        Condition.Build(new Condition.CustomCondition(s => s.BlockTrigger is RemoveUnitChangeEvent { Reason: UnitRemovalReason.BATTLE }), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.TypedDiscardStep(this, CardType.RESPONSE, "Discard a Response card so the Army is not removed by that battle"),
        new BlockStep<RemoveUnitChangeEvent>(this, async _ => {
            await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} uses Code of Bushido: the Army is not removed by that battle").Apply();
            await Task.Delay(GameSettings.DurationMedium);
            return CardStepResult.Block();
        })
        .RequiringPreviousStep()
        .WithGuidance("Keep the Army from being removed by that battle")
    };
}
