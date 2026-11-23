using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventFlexibleResources : EventCardLogic
{
    /// <summary>
    /// The cards this may play out of the discard pile: everything in it EXCEPT this card.
    ///
    /// The exclusion is not a special case, it is the whole reason this helper exists. An Event is
    /// discarded by DeckState.PlayCard the moment it is played — before its steps run — so by the
    /// time the step below asks for the pile, this card is already sitting in it and would offer
    /// itself. Choosing it recycles the card to hand and re-plays it, which re-discards it and offers
    /// it again: a card that plays itself forever.
    ///
    /// Used by the step condition as well as the offer, so a pile holding nothing but this card
    /// leaves the step unexecutable rather than raising a prompt with no valid choice.
    /// </summary>
    private List<int> PlayableDiscardedCardIds(BoardState board) =>
        board.ForFaction(Faction).Discarded.Where(id => id != CardState.Id).ToList();

    /// <summary>
    /// The discard pile this may reach into. Card targets name no board space, so this lights nothing
    /// on the map today — it is declared because the offer is genuinely a target set, and the CLI and
    /// any future card-strip preview read the same field.
    /// </summary>
    public override TargetSet Targets() => TargetSet.Cards(PlayableDiscardedCardIds(BoardState.Live));

    /// <summary>The discard-pile card chosen by the first step, played by the second.</summary>
    private int _selectedCardId = -1;

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new RequirementStep(this, async () => {
                // A single-card pick under this card's own wording. CardsRequestHandler (used before)
                // is the discard prompt: it titled the modal "Select card(s) to discard" and allowed
                // any number of cards, of which only the first was ever played.
                var resp = await new InputRequest.SelectCardRequestHandler(Faction, PlayableDiscardedCardIds(BoardState.Live),
                    "Choose a card from your discard pile to play").BroadCast();

                // Mandatory (the card text has no "may"), so the modal has no Cancel and the CLI/bots
                // get PassMode.NotAllowed. Kept as a defensive guard: an empty answer must never reach
                // the [0] index below.
                if (resp.ResponseCardIds.Count == 0) throw new StepSkippedException();

                _selectedCardId = resp.ResponseCardIds[0];
                return new RecycleCardChangeEvent(Faction, Faction, _selectedCardId, RecycleDestination.Hand);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
                PlayableDiscardedCardIds(s.Board).Count > 0), this))
            .WithGuidance("Play a card of its choice from its discard pile"),

            new PlayCardStep(this, () => Task.FromResult(CardStepResult.PlayCard(_selectedCardId)))
            .RequiringPreviousStep()
        };
    }
}
