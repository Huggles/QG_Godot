using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class StatusRadar : StatusCardLogic
{
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
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 2));
                discardEvent.IsTrigger = false;
                await discardEvent.Apply();

                if (CardPlayPool.LastNoneNewCardChangeEvent is RemoveUnitChangeEvent removeEvent) {
                    removeEvent.IsBlocked = true;
                    removeEvent.UnitState.ImmuneForTurn = true;
                    PresentationServices.Notification.ShowActionText("Radar: US Navy will not be removed this turn", Faction);
                    await Task.Delay(GameSettings.DurationMedium);
                }
                return null;
            })
            .WithGuidance("Discard top 2 deck cards to prevent your Navy from being removed")
        };
    }
}