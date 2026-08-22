using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// "Use once per turn at the beginning of your Discard step. Take 1 or 2 cards from your hand and
/// place them on the bottom of your draw deck."
///
/// A BEFORE-DISCARD step mutator, not an activatable card. The card text puts this *before* the
/// discard, and there is no reaction window there to be offered in: GameFlow.BeginStep applies the
/// step's ChangeStepChangeEvent and then runs the BEFORE mutators, and the discard handler's own
/// DiscardHandCardsChangeEvent carries IsTrigger = false. The previous version triggered on
/// IsGameFlowStep(DISCARD) and so could only ever have been offered by some unrelated event opening a
/// window inside the step — after the discard it was supposed to precede, if at all.
///
/// The class IS the mutator (see the note on StepMutator): it already extends CardLogic, so it
/// implements IStepMutator directly and DeckState.PlayCard registers it when the Status card hits the
/// table, unregistering it when the card leaves. Because it fires once per faction turn from its own
/// step window, "once per turn" needs no activation bookkeeping — and with no CardTriggers() override
/// the played card reads as a passive modifier and is never offered for activation (CardLogic._conditions).
///
/// Using it stays the player's choice: the prompt has a minimum of zero, and taking no card is a
/// decline. StepMutatorRunner also catches StepSkippedException, so a host Skip abandons only this
/// mutator and the normal discard step still follows.
/// </summary>
public partial class StatusRosietheRiveter : StatusCardLogic, IStepMutator
{
    private const int MaxCards = 2;

    public TurnStep      Step   => TurnStep.DISCARD;
    public MutatorTiming Timing => MutatorTiming.BEFORE;

    public string Description =>
        "Rosie the Riveter: place 1 or 2 cards from your hand on the bottom of your draw deck";

    public string BulletinText =>
        "The production lines never stop. Before discarding, take 1 or 2 cards from your hand and "
        + "place them on the bottom of your draw deck.";

    /// <summary>
    /// Only the owner's own Discard step, and only with something to place — an empty hand would
    /// announce the Bulletin and then ask a question with no answers.
    /// </summary>
    public bool ShouldRun(Faction activeFaction) =>
        activeFaction == Faction && DeckState.ForFaction(Faction).HandCardIds.Count > 0;

    public async Task Run(Faction activeFaction)
    {
        List<int> handIds = DeckState.ForFaction(Faction).HandCardIds.ToList();

        InputRequest response = await new InputRequest.SelectCardsRequestHandler(
            Faction,
            handIds,
            "Place 1 or 2 cards from your hand on the bottom of your draw deck",
            minSelections: 0,
            maxSelections: MaxCards).BroadCast();

        // Re-checked against the hand on the host rather than trusted: the answer comes off the wire,
        // and a recycle of a card this faction does not hold would be refused by
        // RecycleCardChangeEvent as an error rather than simply ignored here.
        List<int> selectedIds = response.ResponseCardIds
            .Distinct()
            .Where(handIds.Contains)
            .Take(MaxCards)
            .ToList();

        // Declined — the prompt's minimum is zero, so an empty answer is a legitimate "not this turn".
        if (selectedIds.Count == 0) return;

        foreach (int cardId in selectedIds)
        {
            // isTrigger:false, like MutatorRecycleAfterStep: moving your own hand cards into your own
            // deck is bookkeeping. No block card in the game matches a recycle, so a window per card
            // would only raise always-ask prompts at every faction.
            //
            // The card no longer passes through the discard pile on the way. RecycleCardChangeEvent
            // takes a card out of whichever pile holds it, so the DiscardHandCardsChangeEvent this
            // used to apply first was both unnecessary and wrong — it filed the cards as discarded
            // this turn before moving them out again.
            await this.Do(
                BuildChangeEvent(new RecycleCardChangeEvent(
                    Faction, Faction, cardId, RecycleDestination.BottomOfDeck)),
                isTrigger: false);
        }
    }
}
