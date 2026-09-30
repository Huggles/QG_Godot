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
            Condition.Build(new Condition.CardHasNotBeenActivatedThisTurn(CardState), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // The cost.
            //
            // Deliberately carries NO step condition. The obvious one — "only if the trigger is a
            // removal", which is what the fused step's `if (ActivationTrigger is not
            // RemoveUnitChangeEvent) return;` guard said — is a trap here, and an expensive one:
            // step conditions are evaluated by GameStateCalculator.EvaluateExecutableSteps BEFORE
            // the card is activated, and ActivationTrigger is bound by CardPlayRound.DoCard only
            // once it HAS been. Pre-activation it is null, so such a condition is always false, the
            // step is never IsExecutable, HasExecutableCardSteps is false, and CanBeActivated stops
            // offering the card entirely. Measured: Radar silently disappeared from every game.
            //
            // A step condition that must consult the trigger has to read CardLogic.BlockContext
            // (or TriggerContext), which fall back to the live window. Here none is needed at all:
            // Condition.UnitAboutToBeRemoved in CardTriggers already guarantees the event type, and
            // the BlockStep below re-checks it anyway.
            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 2)))
                .WithGuidance("Discard top 2 deck cards"),

            // The block. RequiringPreviousStep is what keeps the Navy from being saved for free when
            // the discard does not happen — and BlockStep's own cast replaces the ActivationTrigger
            // guard above. Note the removal is re-read from ActivationTrigger AFTER the discard has
            // applied, which is safe precisely because it is not a pool read: the discard's Apply()
            // registers itself into ChangeEventsPool and would have moved LastNoneNewCardChangeEvent
            // off the removal this card exists to block.
            new BlockStep<RemoveUnitChangeEvent>(this, async removeEvent => {
                removeEvent.UnitState.ImmuneForTurn = true;
                PresentationServices.Notification.ShowActionText("Radar: US Navy will not be removed this turn", Faction);
                await Task.Delay(GameSettings.DurationMedium);
                return CardStepResult.Block();
            })
            .RequiringPreviousStep()
            .WithGuidance("Prevent your Navy from being removed")
        };
    }
}