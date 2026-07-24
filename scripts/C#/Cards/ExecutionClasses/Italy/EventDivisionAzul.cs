using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class EventDivisionAzul : EventCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async () => {
                var sovietResponseCardIds = DeckState.ForFaction(Faction.SOVIET).ResponseCardIds;
                int randomIndex = new Random().Next(sovietResponseCardIds.Count);
                int randomCardId = sovietResponseCardIds[randomIndex];

                DiscardHandCardsChangeEvent discardEvent = BuildChangeEvent(
                    new DiscardHandCardsChangeEvent(Faction, Faction.SOVIET, new List<int> { randomCardId }))
                    .WithoutAnimations();
                discardEvent.IsTrigger = true;

                PresentationServices.Notification.ShowActionText("Division Azul: A random Soviet Response card has been discarded.", Faction);
                await Task.Delay(GameSettings.DurationMedium);

                return discardEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
                DeckState.ForFaction(Faction.SOVIET).ResponseCardIds.Count > 0), this))
            .WithGuidance("Discard a random Soviet Response card from the table")
        };
    }
}