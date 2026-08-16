using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// "Use during your Play step after playing a card. Instead of placing that card in your discard pile,
/// shuffle it into your draw deck."
///
/// The recycle is deferred to the end of the turn step via MutatorRecycleAfterStep rather than applied
/// here. Rationing triggers in the played card's introduction window, which fires BEFORE that card's
/// own CardSteps run — recycling on the spot would move the card into the draw deck and only then let
/// it resolve, with Tag.IsPlayed no longer set for it and the card redrawable in the same turn. The
/// card text is about where the card ends up, not whether it resolves.
/// </summary>
public partial class ResponseRationing : ResponseCardLogic
{
    /// <summary>
    /// The played card this activation is about, or null when there is none. Scoped to the event that
    /// opened the current window rather than scanned out of the pool: a pool scan matched any earlier
    /// play of ours still sitting in the discard pile, so the card had to guess which one the window
    /// meant — and guessing wrong recycles the wrong card.
    /// </summary>
    private PlayCardChangeEvent PlayedCard(ChangeEvent trigger) =>
        trigger is PlayCardChangeEvent playCard
        && playCard.TriggeringFaction == Faction
        && DeckState.ForFaction(Faction).DiscardedCardIds.Contains(playCard.SourceCardId)
            ? playCard
            : null;

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.CustomCondition(() =>
                PlayedCard(CardPlayPool.CurrentReactionTrigger) != null
            ).InReactionWindow(), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                // ActivationTrigger, not the pool: DoCard pins the event this activation was offered
                // against, and it survives any Apply() that lands between the card being chosen and
                // this step running.
                PlayCardChangeEvent playEvent = PlayedCard(ActivationTrigger);
                if (playEvent == null)
                {
                    DebugUtilities.PrintPeerError("Rationing: no played card found to recycle");
                    return null;
                }

                ModifierRegistry.Register(new MutatorRecycleAfterStep(
                    Faction,
                    playEvent.SourceCardId,
                    GameFlow.Instance.TurnStep,
                    RecycleDestination.ShuffleIntoDeck,
                    "Rationing: shuffle the played card into the draw deck",
                    "Nothing is wasted. The card just played is shuffled back into its owner's draw deck instead of staying discarded."));

                await Task.CompletedTask;
                return null;   // the mutator does the work once the step ends
            })
            .WithGuidance("Shuffle last played card into your draw deck at the end of this step")
        };
    }
}
