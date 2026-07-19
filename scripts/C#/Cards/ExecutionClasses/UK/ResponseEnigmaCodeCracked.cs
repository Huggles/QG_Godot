using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseEnigmaCodeCracked : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.CustomCondition(() =>
            {
                // DebugUtilities.PrintPeer($"{CardPlayPool.GetChangeEvents<ActivateReactionChangeEvent>().Count}");
                // DebugUtilities.PrintPeer($"{CardPlayPool.GetChangeEvents<ActivateReactionChangeEvent>().Count(ce => ce.TriggeringFaction == Faction.GERMANY)}");
                // DebugUtilities.PrintPeer($"{CardPlayPool.GetChangeEvents<ActivateReactionChangeEvent>().Count(ce => ce.SourceCardState.CardData.CardType == CardType.STATUS)}");
                return CardPlayPool.GetChangeEvents<ActivateReactionChangeEvent>()
                    .Any(ce => ce.TriggeringFaction == Faction.GERMANY
                            && ce.SourceCardState.CardData.CardType == CardType.STATUS);
            }
                
            ), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                var statusActivation = CardPlayPool.GetChangeEvents<ActivateReactionChangeEvent>()
                    .Last(ce => ce.TriggeringFaction == Faction.GERMANY
                             && ce.SourceCardState.CardData.CardType == CardType.STATUS);

                DiscardHandCardsChangeEvent discardEvent = BuildChangeEvent(new DiscardHandCardsChangeEvent(Faction, Faction.GERMANY, new List<int> { statusActivation.SourceCardId }));
                discardEvent.IsTrigger = true;
                return discardEvent;
            })
            .WithGuidance("Discard Germany's Status card")
        };
    }
}
