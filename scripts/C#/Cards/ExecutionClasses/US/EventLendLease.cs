using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class EventLendLease : EventCardLogic
{
    // No Targets() override: what this card offers is a CHOICE OF FACTION, and the board preview
    // draws only Country and Unit targets (InputRequest.PopulateCardTargetPreviews drops the rest).
    // Where the chosen ally then plays is not knowable at hover time — it depends on the card they
    // pick from a hand this card cannot see. TargetSet.Factions would be inert noise.

    /// <summary>
    /// The ally chosen by the first step, read by the draw step. Captured rather than re-prompted:
    /// the play and the draw are separate steps now and the US must not be asked twice.
    /// </summary>
    private Faction _selectedFaction;

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new PlayCardStep(this, async () => {
                var factionResp = await new InputRequest.SelectFactionRequestHandler(
                    Faction, new List<Faction> { Faction.UNITED_KINGDOM, Faction.SOVIET }).BroadCast();
                _selectedFaction = (Faction)factionResp.ResponseCardIds[0];

                // The whole hand, set explicitly: this is a granted out-of-turn play, so the handler's
                // default offer (ActivatableCardIds) is empty for the receiving faction — its play
                // conditions include IsFactionTurn, which fails on the US turn.
                var cardResp = await new InputRequest.HandCardPlayRequestHandler(_selectedFaction)
                {
                    TargetCardIds = DeckState.ForFaction(_selectedFaction).HandCardIds
                }.BroadCast();

                // Nothing, not StepSkippedException: an ally who declines the gift still gets the
                // card draw below, which is what the fused step did. Returning Nothing keeps
                // StepSucceeded true, so the RequiringPreviousStep gate on the draw still opens.
                return cardResp.ResponseCardIds.Count > 0
                    ? CardStepResult.PlayCard(cardResp.ResponseCardIds[0])
                    : CardStepResult.Nothing;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
                s.Board.ForFaction(Faction.UNITED_KINGDOM).Hand.Count > 0 ||
                s.Board.ForFaction(Faction.SOVIET).Hand.Count > 0), this))
            .WithGuidance("Select an Allied faction to play a card and draw a card"),

            // Gated, and the gate is doing real work rather than tidiness: while the ally's card is
            // resolving, step one's StepSucceeded is still false, so a ContinueWithNextSteps fired
            // from inside THAT card's reaction windows cannot hoist this draw into the middle of it.
            new ResultStep(this, Choose.Fixed(_ => new DrawCardsChangeEvent(Faction, _selectedFaction, 1, true)))
            .RequiringPreviousStep()
        };
    }
}
