using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusSuperiorPlanning : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsStartStep(), this),
            Condition.Build(new Condition.CustomCondition(() =>
                DeckState.ForFaction(Faction).DeckCardIds.Count > 0), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                DeckState deck = DeckState.ForFaction(Faction);
                int peekCount = Math.Min(4, deck.DeckCardIds.Count);
                List<int> topCards = deck.DeckCardIds.Take(peekCount).ToList();

                var resp = await new InputRequest.ReorderCardsRequestHandler(Faction, topCards).BroadCast();
                List<int> reorderedIds = resp.ResponseCardIds;

                List<int> fullDeck = new List<int>(reorderedIds);
                fullDeck.AddRange(deck.DeckCardIds.Skip(peekCount));

                // fullDeck is the whole deck because ExecuteAsync replaces DeckCardIds outright, so the
                // event has to be told how much of it the player actually rearranged — otherwise the
                // history reports a four-card peek as a reorder of every card in the deck.
                ReorderDeckChangeEvent reorderEvent = BuildChangeEvent(
                    new ReorderDeckChangeEvent(Faction, fullDeck) { ReorderedFromTop = peekCount });
                await CardPlayPool.DoChangeEvent(reorderEvent);
            }).WithGuidance("Examine and reorder the top 4 cards of your draw deck")
        };
    }
}