using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// "Use when you play a Build Army card for your Play step. Place that Build Army card on the top of
/// your draw deck instead of discarding it."
///
/// Same shape as ResponseRationing, and deferred for the same reason: this fires in the Build Army
/// card's introduction window, before that card's own CardSteps run. Recycling on the spot moved the
/// card to the top of the deck and only then built the army. The text is about where the card ends
/// up, so MutatorRecycleAfterStep does it once the play step has finished.
///
/// A face-up Status card, so the trigger is deliberately strict — only a Soviet Build Army play opens
/// this. The window it produces is public information either way, so it needs no always-ask cover.
/// </summary>
public partial class StatusWomenConscripts : StatusCardLogic
{
    /// <summary>The Build Army card this activation is about, or null when there is none.</summary>
    private PlayCardChangeEvent BuildArmyCard(ChangeEvent trigger) =>
        trigger is PlayCardChangeEvent playCard
        && playCard.TriggeringFaction == Faction
        && CardState.ForId(playCard.SourceCardId)?.CardData.CardType == CardType.BUILD_ARMY
        && DeckState.ForFaction(Faction).DiscardedCardIds.Contains(playCard.SourceCardId)
            ? playCard
            : null;

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            // .InReactionWindow() is load-bearing, not decoration. Without it CustomCondition reports
            // RequiresEventContext = false, so HasEventBasedTrigger is false, so CanBeActivated
            // rejects this card at any ReactionDepth > 0 — which every reaction window is. The card
            // could never be offered at all.
            Condition.Build(new Condition.CustomCondition(() =>
                BuildArmyCard(CardPlayPool.CurrentReactionTrigger) != null
            ).InReactionWindow(), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                PlayCardChangeEvent playEvent = BuildArmyCard(ActivationTrigger);
                if (playEvent == null)
                {
                    DebugUtilities.PrintPeerError("Women Conscripts: no Build Army card found to recycle");
                    return null;
                }

                ModifierRegistry.Register(new MutatorRecycleAfterStep(
                    Faction,
                    playEvent.SourceCardId,
                    GameFlow.Instance.TurnStep,
                    RecycleDestination.TopOfDeck,
                    "Women Conscripts: return the Build Army card to the top of the draw deck",
                    "The call-up never stops. The Build Army card just played goes back on top of its owner's draw deck instead of being discarded."));

                await Task.CompletedTask;
                return null;   // the mutator does the work once the step ends
            })
            .WithGuidance("Place Build Army card on top of your draw deck at the end of this step")
        };
    }
}
