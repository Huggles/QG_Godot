using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class ResponseDestroyers : ResponseCardLogic
{
    /// <summary>The supplied UK or US Navy about to be removed. The block window has already named it, so this card picks nothing.</summary>
    public override TargetSet Targets() => BlockTargets();

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.UnitAboutToBeRemoved(
                [Faction.UNITED_KINGDOM, Faction.UNITED_STATES], UnitType.NAVY, requireInSupply: true), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new BlockStep<RemoveUnitChangeEvent>(this, async removeEvent => {
                removeEvent.UnitState.ImmuneForTurn = true;
                await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} plays Destroyers: the Navy will not be removed this turn").Apply();
                await Task.Delay(GameSettings.DurationMedium);
                return CardStepResult.Block();
            })
            .WithGuidance("Protect the supplied Navy from removal this turn")
        };
    }
}