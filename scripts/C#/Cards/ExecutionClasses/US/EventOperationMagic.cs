using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class EventOperationMagic : EventCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async () => {
                var japaneseResponseCardIds = DeckState.ForFaction(Faction.JAPAN).ResponseCardIds;
                int randomIndex = GameRandom.Next(japaneseResponseCardIds.Count);
                int randomCardId = japaneseResponseCardIds[randomIndex];

                DiscardHandCardsChangeEvent discardEvent = BuildChangeEvent(
                    new DiscardHandCardsChangeEvent(Faction, Faction.JAPAN, new List<int> { randomCardId }))
                    .WithoutAnimations();
                discardEvent.IsTrigger = true;

                PresentationServices.Notification.ShowActionText("Operation Magic: A random Japanese Response card has been discarded.", Faction);
                await Task.Delay(GameSettings.DurationMedium);

                await CardPlayPool.DoChangeEvent(discardEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
                DeckState.ForFaction(Faction.JAPAN).ResponseCardIds.Count > 0), this))
            .WithGuidance("Discard a random Japanese Response card from the table")
        };
    }
}