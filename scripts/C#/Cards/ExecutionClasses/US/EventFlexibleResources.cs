using System;
using System.Collections.Generic;
using Godot;

public partial class EventFlexibleResources : EventCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async () => {
                var discardedIds = DeckState.ForFaction(Faction).DiscardedCardIds;
                var resp = await new InputRequest.CardsRequestHandler(Faction, discardedIds).BroadCast();
                int selectedCardId = resp.ResponseCardIds[0];

                RecycleCardChangeEvent recycleEvent = BuildChangeEvent(
                    new RecycleCardChangeEvent(Faction, Faction, selectedCardId, RecycleDestination.Hand));
                recycleEvent.IsTrigger = false;
                await recycleEvent.ApplyChange();

                await CardPlayPool.DoCard(selectedCardId);
                return null;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
                DeckState.ForFaction(Faction).DiscardedCardIds.Count > 0), this))
            .WithGuidance("Play a card of your choice from your discard pile")
        };
    }
}