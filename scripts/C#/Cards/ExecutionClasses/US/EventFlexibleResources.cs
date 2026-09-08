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
    private List<int> PlayableDiscardedCardIds =>
        DeckState.ForFaction(Faction).DiscardedCardIds.Where(id => id != CardState.Id).ToList();

    /// <summary>
    /// The discard pile this may reach into. Card targets name no board space, so this lights nothing
    /// on the map today — it is declared because the offer is genuinely a target set, and the CLI and
    /// any future card-strip preview read the same field.
    /// </summary>
    public override TargetSet Targets() => TargetSet.Cards(PlayableDiscardedCardIds);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async () => {
                var resp = await new InputRequest.CardsRequestHandler(Faction, PlayableDiscardedCardIds).BroadCast();

                // Declining leaves an EMPTY response rather than setting WasSkipped — that is
                // CardsRequestHandler's pass idiom (PassMode.EmptyResponse), so BroadCast does not
                // throw for it and this is the only place the decline can be noticed. Without the
                // guard the next line indexed [0] on an empty list and took the turn loop down.
                if (resp.ResponseCardIds.Count == 0) throw new StepSkippedException();

                int selectedCardId = resp.ResponseCardIds[0];

                RecycleCardChangeEvent recycleEvent = BuildChangeEvent(
                    new RecycleCardChangeEvent(Faction, Faction, selectedCardId, RecycleDestination.Hand));
                recycleEvent.IsTrigger = false;
                await recycleEvent.Apply();

                await CardPlayPool.DoCard(selectedCardId);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
                PlayableDiscardedCardIds.Count > 0), this))
            .WithGuidance("Play a card of your choice from your discard pile")
        };
    }
}
