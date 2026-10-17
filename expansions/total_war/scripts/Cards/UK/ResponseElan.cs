using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// "Use when a French Army is about to be removed. Do not remove that Army this turn." Any removal the
/// block window offers: a battle, a card's elimination, or a supply loss in the UK's Supply step. The
/// protection then holds against every removal for the rest of the turn (CardPlayRound.DoChangeEvent).
/// </summary>
public partial class ResponseElan : ResponseCardLogic
{
    /// <summary>The French Army about to be removed. The block window has already named it, so this card picks nothing.</summary>
    public override TargetSet Targets() => BlockTargets();

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsBlockRequest(), this),
        Condition.Build(new Condition.UnitAboutToBeRemoved(Faction.FRANCE, UnitType.ARMY), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new BlockStep<RemoveUnitChangeEvent>(this, async removeEvent => {
            await new ProtectUnitsChangeEvent(Faction, new List<int> { removeEvent.UnitState.Id }).Apply();
            await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} plays Élan: the French Army will not be removed this turn").Apply();
            await Task.Delay(GameSettings.DurationMedium);
            return CardStepResult.Block();
        })
        .WithGuidance("Protect the French Army from removal this turn")
    };
}
