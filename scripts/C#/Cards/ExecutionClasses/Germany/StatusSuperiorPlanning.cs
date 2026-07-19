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

                // Apply reorder directly to deck state
                for (int i = 0; i < peekCount; i++)
                    deck.DeckCardIds.RemoveAt(0);
                deck.DeckCardIds.InsertRange(0, reorderedIds);

                return null;
            }).WithGuidance("Examine and reorder the top 4 cards of your draw deck")
        };
    }
}