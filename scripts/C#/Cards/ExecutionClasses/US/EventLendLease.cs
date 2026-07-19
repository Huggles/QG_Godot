using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class EventLendLease : EWCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async () => {
                var factionResp = await new InputRequest.SelectFactionRequestHandler(
                    Faction, new List<Faction> { Faction.UNITED_KINGDOM, Faction.SOVIET }).BroadCast();
                Faction selectedFaction = (Faction)factionResp.ResponseCardIds[0];

                var cardResp = await new InputRequest.HandCardPlayRequestHandler(selectedFaction).BroadCast();
                if (cardResp.ResponseCardIds.Count > 0)
                    await CardPlayPool.DoCard(cardResp.ResponseCardIds[0]);

                DrawCardsChangeEvent drawEvent = BuildChangeEvent(new DrawCardsChangeEvent(Faction, selectedFaction, 1, true));
                drawEvent.IsTrigger = true;
                return drawEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
                DeckState.ForFaction(Faction.UNITED_KINGDOM).HandCardIds.Count > 0 ||
                DeckState.ForFaction(Faction.SOVIET).HandCardIds.Count > 0), this))
            .WithGuidance("Select an Allied faction to play a card and draw a card")
        };
    }
}