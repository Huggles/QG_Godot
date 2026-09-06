using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class EventLendLease : EventCardLogic
{
    // No Targets() override: what this card offers is a CHOICE OF FACTION, and the board preview
    // draws only Country and Unit targets (InputRequest.PopulateCardTargetPreviews drops the rest).
    // Where the chosen ally then plays is not knowable at hover time — it depends on the card they
    // pick from a hand this card cannot see. TargetSet.Factions would be inert noise.

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async () => {
                var factionResp = await new InputRequest.SelectFactionRequestHandler(
                    Faction, new List<Faction> { Faction.UNITED_KINGDOM, Faction.SOVIET }).BroadCast();
                Faction selectedFaction = (Faction)factionResp.ResponseCardIds[0];

                // The whole hand, set explicitly: this is a granted out-of-turn play, so the handler's
                // default offer (ActivatableCardIds) is empty for the receiving faction — its play
                // conditions include IsFactionTurn, which fails on the US turn.
                var cardResp = await new InputRequest.HandCardPlayRequestHandler(selectedFaction)
                {
                    TargetCardIds = DeckState.ForFaction(selectedFaction).HandCardIds
                }.BroadCast();
                if (cardResp.ResponseCardIds.Count > 0)
                    await CardPlayPool.DoCard(cardResp.ResponseCardIds[0]);

                DrawCardsChangeEvent drawEvent = BuildChangeEvent(new DrawCardsChangeEvent(Faction, selectedFaction, 1, true));
                drawEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(drawEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
                DeckState.ForFaction(Faction.UNITED_KINGDOM).HandCardIds.Count > 0 ||
                DeckState.ForFaction(Faction.SOVIET).HandCardIds.Count > 0), this))
            .WithGuidance("Select an Allied faction to play a card and draw a card")
        };
    }
}