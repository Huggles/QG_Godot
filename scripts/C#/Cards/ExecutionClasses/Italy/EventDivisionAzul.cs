using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class EventDivisionAzul : EventCardLogic
{
    // No Targets() override, deliberately. This discards a RANDOM Soviet Response card sight
    // unseen, and those cards are face down. Targets() is evaluated on the host and its result is
    // put on the wire in InputRequest.CardTargetPreviews, so naming the pile here would hand the
    // Italian player the identity of cards the rules keep hidden. TargetSet.None is the correct
    // answer, not a gap to be filled.

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, async () => {
                var sovietResponseCardIds = DeckState.ForFaction(Faction.SOVIET).ResponseCardIds;
                int randomIndex = GameRandom.Next(sovietResponseCardIds.Count);
                int randomCardId = sovietResponseCardIds[randomIndex];

                DiscardHandCardsChangeEvent discardEvent = 
                    new DiscardHandCardsChangeEvent(Faction, Faction.SOVIET, new List<int> { randomCardId })
                    .WithoutAnimations();

                await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} plays Division Azul: a random Soviet Response card is discarded").Apply();
                await Task.Delay(GameSettings.DurationMedium);

                return discardEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
                s.Board.ForFaction(Faction.SOVIET).Response.Count > 0), this))
            .WithGuidance("Discard a random Soviet Response card from the table")
        };
    }
}