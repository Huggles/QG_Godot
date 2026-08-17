using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The one action every faction always has beside its hand: instead of playing a card, discard 4 cards
/// from hand and take a Build or Battle card straight out of the draw deck, playing it immediately.
///
/// Not a scenario rule — unlike MutatorSyntheticFuelAnyFaction this is registered unconditionally by
/// GameModeMultiplayerDefault.RegisterMutators rather than declared in a scenario's "mutators" array,
/// so no scenario can omit it. It is a Bulletin for the usual reason (see ActivatableMutator): it must
/// present and activate as a card without being one — no entry in QGData_Cards_V2.json, in no deck
/// pile, never discarded, and nothing may react to its use.
///
/// Nothing can trigger on the activation itself: DoCard builds the ActivateReactionChangeEvent with
/// IsTrigger = false and opens an activation window only for a PlayCardChangeEvent, and every event
/// the step below emits is IsTrigger = false. The one deliberate exception is the taken card's own
/// play, which goes through CardPlayPool.DoCard and triggers responses exactly like any other play.
/// </summary>
public partial class MutatorReallocateResources : ActivatableMutator
{
    /// <summary>How many hand cards the action costs. Also the availability gate below.</summary>
    private const int DiscardCost = 4;

    private static readonly List<CardType> DrawableTypes = new()
        { CardType.BUILD_ARMY, CardType.BUILD_NAVY, CardType.LAND_BATTLE, CardType.SEA_BATTLE };

    public override string Label => "Reallocate Resources";

    /// <summary>
    /// Computed live rather than fixed, and the reason CardState.DisplayText exists: naming the cards
    /// still in the draw deck is only useful if the player can read it off the card face BEFORE
    /// activating, since the activation cannot be undone once it starts. Reconstructing the same answer
    /// from the discard pile is possible but tedious, so the card says it.
    ///
    /// Reads only replicated state (DeckCardIds) and static CardData, so every peer renders the same
    /// text — see the note on ActivatableMutator.Text.
    /// </summary>
    public override string Text
    {
        get
        {
            List<string> available = DrawOptions()
                .Select(cardId => CardState.ForId(cardId).CardData.Label)
                .Distinct()
                .ToList();

            return $"Instead of playing a card: discard {DiscardCost} cards from your hand, then take "
                 + "one of these from your draw deck and play it immediately.\n\n"
                 + (available.Count > 0
                        ? $"Available: {string.Join(", ", available)}."
                        : "Nothing left in your draw deck to take.");
        }
    }

    /// <summary>
    /// One representative draw-deck card per distinct card name, in deck order.
    ///
    /// Deduped because a deck holds several identical Build Army cards and the prompt should offer the
    /// choice once, not once per copy. The representative is a real deck card id, so DoCard plays that
    /// exact card and no separate draw is needed.
    /// </summary>
    private List<int> DrawOptions() =>
        DeckState.ForFaction(Faction).DeckCardIds
            .Where(cardId => DrawableTypes.Contains(CardState.ForId(cardId).CardData.CardType))
            .GroupBy(cardId => CardState.ForId(cardId).CardData.UniqueName)
            .Select(sameNamedCards => sameNamedCards.First())
            .ToList();

    /// <summary>
    /// No event-scoped condition here, deliberately — unlike MutatorSyntheticFuelAnyFaction this is a
    /// Play-step action, not a reaction, and CardLogic.CanBeActivated offering it only at
    /// ReactionDepth 0 is exactly what is wanted.
    /// </summary>
    protected override List<Condition> MutatorTriggers()
    {
        return new List<Condition> {
            // IsPlayCardStep is both the availability gate (it folds in "the play is not yet spent")
            // and the marker CardLogic.IsPlayStepActivation reads to place this beside the hand.
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.CustomCondition(() =>
                DeckState.ForFaction(Faction).HandCardIds.Count >= DiscardCost), this),
            // Nothing left to take would make the discard a pure loss, so the action is unavailable
            // (drawn greyed out) rather than a trap.
            Condition.Build(new Condition.CustomCondition(() => DrawOptions().Count > 0), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                // Spent up front: the play must already read as gone while the prompts below are open,
                // and a host timeout can end the step with no card played at all. The taken card's own
                // PlayCardChangeEvent increments the same counter again, which is harmless —
                // CardsPlayedThisTurnStep is only ever tested > 0 and is cleared each round.
                SpendPlayActionChangeEvent spendEvent = BuildChangeEvent(new SpendPlayActionChangeEvent(Faction));
                spendEvent.IsTrigger = false;
                await spendEvent.Apply();

                // Raises its own required selection and round-trips the picks to clients.
                ForceDiscardHandCardsChangeEvent discardEvent =
                    BuildChangeEvent(new ForceDiscardHandCardsChangeEvent(Faction, Faction, DiscardCost));
                discardEvent.IsTrigger = false;
                await discardEvent.Apply();

                // Recomputed rather than captured before the discard: the discard only moves hand
                // cards so the answer is the same, but the deck is live state and the read belongs
                // next to its use.
                InputRequest pick = await new InputRequest.SelectCardRequestHandler(
                    Faction, DrawOptions(), "Take a card from your draw deck and play it").BroadCast();

                // Only reachable through a host timeout / Skip decision — the modal has no Cancel
                // while the pick is required, and MutatorTriggers guarantees at least one option.
                if (pick.ResponseCardIds.Count == 0) return;

                // The full pipeline, deliberately: this play triggers responses like any other.
                // DeckState.PlayCard takes the card out of DeckCardIds, so the card goes from deck to
                // table without ever passing through hand.
                await CardPlayPool.DoCard(pick.ResponseCardIds[0]);
            })
            .WithGuidance($"Discard {DiscardCost} cards to take a Build or Battle card from your deck and play it")
        };
    }
}
