using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// "Use when Germany activates a Status card. Discard that Status card."
///
/// A BLOCK reaction, hosted in the block window of the first blockable step of the Status card being
/// activated. It does not block: the Status card resolves this one activation and then leaves the
/// table, so Germany cannot use it again. That is what the card always did in practice — discarding a
/// card has never halted an activation in flight, because DoCard's step loop reads only StepFinished
/// and CardLogic.IsBlocked, and DeckState.DiscardCard touches neither.
///
/// It lives on the block axis for its TIMING, not to block: the block window is the only reaction
/// window that opens before the activated card's effect applies. It previously keyed on
/// Condition.CardActivated(...).Immediately() in the activation window DoCard opened on the
/// introduction event; that window is gone, and it had in any case been unreachable since the window
/// was narrowed to PlayCardChangeEvent — CardActivated only ever matched ActivateReactionChangeEvent.
///
/// Consequence to know: every activatable Germany Status card pays its cost with an IsTrigger = false
/// event first and emits its blockable payload second, so by the time this can fire Germany has
/// already spent the discard. A Status card whose steps produce no blockable event at all opens no
/// window, and this card is simply not offered against it.
/// </summary>
public partial class ResponseEnigmaCodeCracked : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsBlockRequestFromCard(Faction.GERMANY, CardType.STATUS), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                // ActivationTrigger, not CurrentReactionTrigger: DoCard pins the event this activation
                // was offered against — here the step event being blocked — and it survives any Apply()
                // landing between the card being chosen and this step running. CurrentReactionTrigger is
                // not even set inside a block window.
                ChangeEvent blockedEvent = ActivationTrigger;
                if (blockedEvent == null || blockedEvent.SourceCardId < 0)
                {
                    DebugUtilities.PrintPeerError("Enigma Code Cracked: no source card on the blocked event");
                    return;
                }

                DiscardHandCardsChangeEvent discardEvent = BuildChangeEvent(
                    new DiscardHandCardsChangeEvent(Faction, Faction.GERMANY, new List<int> { blockedEvent.SourceCardId }));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
            .WithGuidance("Discard Germany's Status card")
        };
    }
}
