using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class StatusRadar : StatusCardLogic
{
    /// <summary>The supplied US Navy about to be removed. The block window has already named it,
    /// so this card picks nothing.</summary>
    public override TargetSet Targets() => BlockTargets();

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequest(), this),
            Condition.Build(new Condition.UnitAboutToBeRemoved(Faction, UnitType.NAVY, requireInSupply: true), this),
            // "When your supplied Navy is battled": an elimination (Kamikaze) or supply loss is not a battle.
            Condition.Build(new Condition.CustomCondition(s => s.BlockTrigger is RemoveUnitChangeEvent { Reason: UnitRemovalReason.BATTLE }), this),
            Condition.Build(new Condition.CardHasNotBeenActivatedThisTurn(CardState), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // No step condition, deliberately: one reading ActivationTrigger is always false before
            // activation, and Radar would never be offered (it vanished from every game that way).
            // UnitAboutToBeRemoved in CardTriggers scopes it, and BlockStep re-checks the type. The
            // removal is read from ActivationTrigger, not the pool, which the price's Apply() moves.
            new BlockStep<RemoveUnitChangeEvent>(this, async removeEvent => {
                await new ProtectUnitsChangeEvent(Faction, new List<int> { removeEvent.UnitState.Id }).Apply();
                await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} plays Radar: the US Navy will not be removed this turn").Apply();
                await Task.Delay(GameSettings.DurationMedium);
                return CardStepResult.Block();
            })
            .WithPrice(StepPrice.DeckDiscard(this, 2))
            .WithGuidance("Discard top 2 deck cards to protect its Navy from removal")
        };
    }
}