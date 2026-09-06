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
            new CardStep(this, async () => {
                // Resolved before the discard, and from ActivationTrigger rather than the pool:
                // discardEvent.Apply() registers itself into the round's ChangeEventsPool, which
                // would move LastNoneNewCardChangeEvent off the removal this card is blocking.
                if (ActivationTrigger is not RemoveUnitChangeEvent removeEvent) return;

                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 2));
                discardEvent.IsTrigger = false;
                await discardEvent.Apply();

                removeEvent.IsBlocked = true;
                removeEvent.UnitState.ImmuneForTurn = true;
                PresentationServices.Notification.ShowActionText("Radar: US Navy will not be removed this turn", Faction);
                await Task.Delay(GameSettings.DurationMedium);
            })
            .WithGuidance("Discard top 2 deck cards to prevent your Navy from being removed")
        };
    }
}