using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseRationing : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.CustomCondition(() =>
                CardPlayPool.GetChangeEvents<PlayCardChangeEvent>().Any(ce =>
                    ce.TriggeringFaction == Faction &&
                    DeckState.ForFaction(Faction).DiscardedCardIds.Contains(ce.SourceCardId))
            ), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                DeckState deckState = DeckState.ForFaction(Faction);
                PlayCardChangeEvent playEvent = CardPlayPool.GetChangeEvents<PlayCardChangeEvent>()
                    .Last(ce =>
                        ce.TriggeringFaction == Faction &&
                        deckState.DiscardedCardIds.Contains(ce.SourceCardId));

                RecycleCardChangeEvent recycleEvent = BuildChangeEvent(new RecycleCardChangeEvent(Faction, Faction, playEvent.SourceCardId, RecycleDestination.ShuffleIntoDeck));
                recycleEvent.IsTrigger = false;
                await recycleEvent.ApplyChange();
                return null;
            })
            .WithGuidance("Shuffle last played card into your draw deck")
        };
    }
}