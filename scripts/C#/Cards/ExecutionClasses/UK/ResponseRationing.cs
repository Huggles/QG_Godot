using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// "Use during your Play step after playing a card. Instead of placing that card in your discard pile,
/// shuffle it into your draw deck."
///
/// The recycle is deferred to the end of the turn step via MutatorRecycleAfterStep rather than applied
/// here. Rationing triggers in the activation window, which fires BEFORE the triggering card's own
/// CardSteps run — recycling on the spot would move the card into the draw deck and only then let it
/// resolve, with Tag.IsPlayed no longer set for it and the card redrawable in the same turn. The card
/// text is about where the card ends up, not whether it resolves.
/// </summary>
public partial class ResponseRationing : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.CustomCondition(() =>
                CardPlayPool.GetChangeEvents<PlayCardChangeEvent>().Any(ce =>
                    ce.TriggeringFaction == Faction &&
                    DeckState.ForFaction(Faction).DiscardedCardIds.Contains(ce.SourceCardId))
            ).InReactionWindow(), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                DeckState deckState = DeckState.ForFaction(Faction);

                bool IsOwnDiscardedPlay(PlayCardChangeEvent ce) =>
                    ce != null &&
                    ce.TriggeringFaction == Faction &&
                    deckState.DiscardedCardIds.Contains(ce.SourceCardId);

                // Prefer the event that opened this window, but only when it is OUR play — the window
                // may have been opened by another faction's card while the pool-based trigger matched
                // an earlier play of ours. Recycling then would pull an enemy card into our deck.
                // Otherwise fall back to the same pool scan CardTriggers uses.
                PlayCardChangeEvent trigger = CardPlayPool.CurrentReactionTrigger as PlayCardChangeEvent;
                PlayCardChangeEvent playEvent = IsOwnDiscardedPlay(trigger)
                    ? trigger
                    : CardPlayPool.GetChangeEvents<PlayCardChangeEvent>().LastOrDefault(IsOwnDiscardedPlay);

                if (playEvent == null)
                {
                    DebugUtilities.PrintPeerError("Rationing: no played card found to recycle");
                    return null;
                }

                ModifierRegistry.Register(new MutatorRecycleAfterStep(
                    Faction, playEvent.SourceCardId, GameFlow.Instance.TurnStep));

                await Task.CompletedTask;
                return null;   // the mutator does the work once the step ends
            })
            .WithGuidance("Shuffle last played card into your draw deck at the end of this step")
        };
    }
}
