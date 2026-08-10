using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class StatusWomenConscripts : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.CustomCondition(() => {
                if (CardPlayPool.CurrentReactionTrigger is not PlayCardChangeEvent playCard) return false;
                return playCard.TriggeringFaction == Faction
                    && CardState.ForId(playCard.SourceCardId).CardData.CardType == CardType.BUILD_ARMY;
            }), this)
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
                        CardState.ForId(ce.SourceCardId).CardData.CardType == CardType.BUILD_ARMY &&
                        deckState.DiscardedCardIds.Contains(ce.SourceCardId));

                RecycleCardChangeEvent recycleEvent = BuildChangeEvent(new RecycleCardChangeEvent(Faction, Faction, playEvent.SourceCardId, RecycleDestination.TopOfDeck));
                recycleEvent.IsTrigger = false;
                await recycleEvent.Apply();
                return null;
            })
            .WithGuidance("Place Build Army card on top of draw deck")
        };
    }
}