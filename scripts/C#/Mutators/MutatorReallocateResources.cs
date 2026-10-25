using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The one action every faction always has beside its hand: instead of playing a card, discard cards
/// from hand (4 in the base game, see <see cref="DiscardCostFor"/>) and take a Build or Battle card
/// straight out of the draw deck, playing it immediately.
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
    private static readonly List<CardType> DrawableTypes = new()
        { CardType.BUILD_ARMY, CardType.BUILD_NAVY, CardType.LAND_BATTLE, CardType.SEA_BATTLE };

    /// <summary>
    /// How many hand cards the action costs <paramref name="faction"/> on <paramref name="board"/>: the session's
    /// base, then each of its IReallocateCostModifier cards in play. Also the availability gate below. Public
    /// so an expansion's variant of this action (Total War's War Bonds) charges the same.
    /// </summary>
    public static int DiscardCostFor(Faction faction, BoardState board) =>
        board.Modifiers<IReallocateCostModifier>()
            .Where(modifier => modifier.Faction == faction)
            .Aggregate(GameOptions.Rules.ReallocateDiscardCost, (cost, modifier) => modifier.ReallocateCost(cost));

    private int DiscardCost(BoardState board) => DiscardCostFor(Faction, board);

    /// <summary>
    /// The cards of <paramref name="pile"/> this action may take: Build and Battle cards that could do
    /// something if played now, one per name. Public for the same reason as <see cref="DiscardCostFor"/>.
    /// </summary>
    public static List<int> TakeOptions(IEnumerable<int> pile, GameSituation situation) =>
        pile.Where(cardId => DrawableTypes.Contains(CardState.ForId(cardId).CardData.CardType))
            .Where(cardId => CanPlayNow(cardId, situation))
            .GroupBy(cardId => CardState.ForId(cardId).CardData.UniqueName)
            .Select(sameNamedCards => sameNamedCards.First())
            .ToList();

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
            // The count is every copy left in the deck, not just the one representative DrawOptions keeps.
            List<int> deck = BoardState.Live.ForFaction(Faction).Deck;
            List<string> available = DrawOptions(GameSituation.Live)
                .Select(cardId => CardState.ForId(cardId).CardData.Label)
                .Distinct()
                .Select(label => $"{label} ({deck.Count(cardId => CardState.ForId(cardId).CardData.Label == label)})")
                .ToList();

            int cost = DiscardCost(BoardState.Live);
            return $"Instead of playing a card: discard {cost} card{(cost == 1 ? "" : "s")} from your hand, then take "
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
    // Only cards that could actually do something if played now — a Build Navy with no sea to build in
    // is not offered, so the four discarded cards can never buy a play that does nothing.
    private List<int> DrawOptions(GameSituation situation) => TakeOptions(situation.Board.ForFaction(Faction).Deck, situation);

    /// <summary>
    /// No event-scoped condition here, deliberately — unlike MutatorSyntheticFuelAnyFaction this is a
    /// Play-step action, not a reaction, and CardLogic.CanBeActivated offering it only at
    /// ReactionDepth 0 is exactly what is wanted.
    /// </summary>
    /// <summary>Whether the card's first step could run right now: its conditions, e.g. HasBuildableSea for Build Navy.</summary>
    private static bool CanPlayNow(int cardId, GameSituation situation)
        => CardState.ForId(cardId).CardLogic?.CardSteps.FirstOrDefault()?.MeetAllConditions(situation) ?? false;

    protected override List<Condition> MutatorTriggers()
    {
        return new List<Condition> {
            // IsPlayCardStep is both the availability gate (it folds in "the play is not yet spent")
            // and the marker CardLogic.IsPlayStepActivation reads to place this beside the hand.
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.CustomCondition(s =>
                s.Board.ForFaction(Faction).Hand.Count >= DiscardCost(s.Board)), this),
            // Nothing left to take would make the discard a pure loss, so the action is unavailable
            // (drawn greyed out) rather than a trap.
            Condition.Build(new Condition.CustomCondition(s => DrawOptions(s).Count > 0), this)
        };
    }

    /// <summary>
    /// This action reaches cards, not board spaces, so it declares Card targets and the board stays
    /// dark — the hover preview draws only Country and Unit targets. Declared anyway
    /// rather than left as None: it is the truthful answer, and a future presentation that highlights
    /// deck cards gets it for free.
    /// </summary>
    public override TargetSet Targets() => TargetSet.Cards(DrawOptions(GameSituation.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Spent up front: the play must already read as gone while the prompts below are open,
            // and a host timeout can end the sequence with no card played at all. The taken card's own
            // PlayCardChangeEvent increments the same counter again, which is harmless —
            // CardsPlayedThisTurnStep is only ever tested > 0 and is cleared each round.
            new RequirementStep(this, Choose.Fixed(_ => new SpendPlayActionChangeEvent(Faction)))
            // No count in the label: the steps are built once, and the cost can change during the game.
            .WithGuidance("Discard cards from its hand to take a Build or Battle card from its deck and play it"),

            // Raises its own required selection and round-trips the picks to clients.
            new RequirementStep(this, Choose.Fixed(c => new ForceDiscardHandCardsChangeEvent(Faction, Faction, DiscardCost(c.Board))))
            .RequiringPreviousStep(),

            // The full pipeline, deliberately: this play triggers responses like any other.
            // DeckState.PlayCard takes the card out of DeckCardIds, so the card goes from deck to
            // table without ever passing through hand.
            //
            // DrawOptions is read here rather than captured before the discard: the discard only
            // moves hand cards so the answer is the same, but the deck is live state and the read
            // belongs next to its use.
            new PlayCardStep(this, PlayChoice.From(c => DrawOptions(c.Situation), "Take a card from your draw deck and play it"))
            .RequiringPreviousStep()
        };
    }
}
