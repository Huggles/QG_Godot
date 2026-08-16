using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusRosietheRiveter : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsGameFlowStep(TurnStep.DISCARD), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.CardHasNotBeenActivatedThisTurn(CardState), this),
            Condition.Build(new Condition.CustomCondition(() => DeckState.ForFaction(Faction).HandCardIds.Count > 0), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                var handIds = DeckState.ForFaction(Faction).HandCardIds.ToList();
                var resp = await new InputRequest.CardsRequestHandler(Faction, handIds).BroadCast();
                var selectedIds = resp.ResponseCardIds.Take(2).ToList();
                if (selectedIds.Count == 0) return;

                DiscardHandCardsChangeEvent discardEvent = BuildChangeEvent(
                    new DiscardHandCardsChangeEvent(Faction, Faction, selectedIds)).WithoutAnimations();
                discardEvent.IsTrigger = false;
                await discardEvent.Apply();

                foreach (int cardId in selectedIds)
                {
                    RecycleCardChangeEvent recycleEvent = BuildChangeEvent(
                        new RecycleCardChangeEvent(Faction, Faction, cardId, RecycleDestination.BottomOfDeck));
                    recycleEvent.IsTrigger = false;
                    await recycleEvent.Apply();
                }

                PresentationServices.Notification.ShowActionText($"{Faction.WithPlayer()} placed {selectedIds.Count} card(s) on the bottom of their deck.", Faction);
                await Task.Delay(GameSettings.DurationMedium);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => DeckState.ForFaction(Faction).HandCardIds.Count > 0), this))
            .WithGuidance("Take 1 or 2 cards from your hand and place them on the bottom of your deck")
        };
    }
}