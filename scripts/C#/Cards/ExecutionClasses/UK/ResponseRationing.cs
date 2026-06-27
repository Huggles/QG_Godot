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

                deckState.DiscardedCardIds.Remove(playEvent.SourceCardId);
                deckState.DeckCardIds.Add(playEvent.SourceCardId);
                deckState.ShuffleDeck();

                PlayerActionLabel.ShowText("Rationing: Card shuffled back into draw deck", Faction);
                await Task.Delay(GameSettings.DurationMedium);
                return null;
            })
            .WithGuidance("Shuffle last played card into your draw deck")
        };
    }
}