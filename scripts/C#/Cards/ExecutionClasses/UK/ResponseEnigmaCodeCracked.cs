using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseEnigmaCodeCracked : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        var trigger = new Condition.CardActivated(Faction.GERMANY, CardType.STATUS);
        trigger.Immediately();
        return new List<Condition> { Condition.Build(trigger, this) };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                var statusActivation = CardPlayPool.CurrentReactionTrigger as ActivateReactionChangeEvent;
                if (statusActivation == null) return;

                DiscardHandCardsChangeEvent discardEvent = BuildChangeEvent(new DiscardHandCardsChangeEvent(Faction, Faction.GERMANY, new List<int> { statusActivation.SourceCardId }));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
            .WithGuidance("Discard Germany's Status card")
        };
    }
}
