using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// "Use during your Play step after playing a card. Instead of placing that card in your discard pile,
/// shuffle it into your draw deck."
///
/// The recycle is deferred to the end of the turn step via MutatorRecycleAfterStep rather than applied
/// here. The card text is about where the played card ends up, not whether it resolves, and this can
/// now fire while that card is still resolving — recycling on the spot would move it into the draw
/// deck and only then let it resolve, with Tag.IsPlayed no longer set for it and the card redrawable
/// in the same turn.
///
/// Pool-scoped, not window-scoped. It used to key on CardPlayPool.CurrentReactionTrigger being the
/// PlayCardChangeEvent itself, which needed an activation window opened on that event for every card
/// play in the game to serve two cards. Condition.FactionPlayedCard scans the round's event pool
/// instead, so this is offered in the after-reaction window of ANY step of the played card.
///
/// IsGameFlowStep + IsFactionTurn carry the "during your Play step" half of the text, and are load
/// bearing under pool scope: a round is one faction's one turn step, and EventLendLease makes the UK
/// play a card inside the US's round, which would otherwise keep this offered for the rest of it.
/// </summary>
public partial class ResponseRationing : ResponseCardLogic
{
    // No Targets() override: this redirects a played card to your draw deck instead of the discard
    // pile. It touches no country and no unit, so there is nothing to light up.

    /// <summary>
    /// The played card this activation is about, or null when there is none.
    ///
    /// The trigger condition and the step must resolve it the SAME way. Under pool scope the window's
    /// trigger is a step event (a deploy, a battle), not the play, so the step can no longer read
    /// ActivationTrigger — it would find no PlayCardChangeEvent and the card would be offered only to
    /// no-op. LastOrDefault, not First: a round can hold two plays by the same faction (see
    /// MutatorReallocateResources, StatusGuards), and the most recent is the one just played.
    ///
    /// The discard-pile clause is what excludes a Status/Response card played onto the table —
    /// DeckState.PlayCard files those into StatusCardIds/ResponseCardIds and only falls through to
    /// DiscardCard for other types. Without it, pool scope would match a table play too.
    /// </summary>
    private PlayCardChangeEvent PlayedCard() =>
        CardPlayPool.ChangeEventsPool
            .OfType<PlayCardChangeEvent>()
            .LastOrDefault(playCard =>
                playCard.TriggeringFaction == Faction
                && DeckState.ForFaction(Faction).DiscardedCardIds.Contains(playCard.SourceCardId));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.FactionPlayedCard(Faction), this),
            Condition.Build(new Condition.IsGameFlowStep(TurnStep.PLAY_CARD), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.CustomCondition(() => PlayedCard() != null), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                PlayCardChangeEvent playEvent = PlayedCard();
                if (playEvent == null)
                {
                    DebugUtilities.PrintPeerError("Rationing: no played card found to recycle");
                    return;
                }

                ModifierRegistry.Register(new MutatorRecycleAfterStep(
                    Faction,
                    playEvent.SourceCardId,
                    CardState.Id,
                    GameFlow.Instance.TurnStep,
                    RecycleDestination.ShuffleIntoDeck,
                    "Rationing: shuffle the played card into the draw deck",
                    "Nothing is wasted. The card just played is shuffled back into its owner's draw deck instead of staying discarded."));

                await Task.CompletedTask;
                // the mutator does the work once the step ends
            })
            .WithGuidance("Shuffle last played card into your draw deck at the end of this step")
        };
    }
}
