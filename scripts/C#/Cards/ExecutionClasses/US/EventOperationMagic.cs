using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class EventOperationMagic : EventCardLogic
{
    // No Targets() override, deliberately — see EventDivisionAzul, which this mirrors for Japan.
    // The discarded Response card is chosen at random from a face-down pile, so declaring it would
    // leak hidden information over the wire.

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, async () => {
                var japaneseResponseCardIds = DeckState.ForFaction(Faction.JAPAN).ResponseCardIds;
                int randomIndex = GameRandom.Next(japaneseResponseCardIds.Count);
                int randomCardId = japaneseResponseCardIds[randomIndex];

                DiscardHandCardsChangeEvent discardEvent = 
                    new DiscardHandCardsChangeEvent(Faction, Faction.JAPAN, new List<int> { randomCardId })
                    .WithoutAnimations();

                await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} plays Operation Magic: a random Japanese Response card is discarded").Apply();
                await Task.Delay(GameSettings.DurationMedium);

                return discardEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
                s.Board.ForFaction(Faction.JAPAN).Response.Count > 0), this))
            .WithGuidance("Discard a random Japanese Response card from the table")
        };
    }
}