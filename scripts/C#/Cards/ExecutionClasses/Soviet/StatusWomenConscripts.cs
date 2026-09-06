using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// "Use when you play a Build Army card for your Play step. Place that Build Army card on the top of
/// your draw deck instead of discarding it."
///
/// Same shape as ResponseRationing, and deferred for the same reason: this can fire while the Build
/// Army card is still resolving, so recycling on the spot would move the card to the top of the deck
/// and only then build the army. The text is about where the card ends up, so MutatorRecycleAfterStep
/// does it once the play step has finished.
///
/// Pool-scoped, not window-scoped. It used to key on CardPlayPool.CurrentReactionTrigger being the
/// PlayCardChangeEvent itself, which needed an activation window opened on that event for every card
/// play in the game to serve two cards. Condition.FactionPlayedCard scans the round's event pool
/// instead, so this is offered in the after-reaction window of ANY step of the Build Army card —
/// in practice the DeployUnitChangeEvent its single step produces.
///
/// IsGameFlowStep + IsFactionTurn carry the "for your Play step" half of the text. They also keep pool
/// scope honest: a round is one faction's one turn step, and EventLendLease plays another faction's
/// hand card inside the US's round.
///
/// A face-up Status card, so its window is public information either way — it never needed always-ask
/// cover, and the StatusGuards path (which plays a Build Army card from inside its own step, on the
/// Soviet's own Play step) still reaches it.
/// </summary>
public partial class StatusWomenConscripts : StatusCardLogic
{
    // No Targets() override: this changes where a played card goes afterwards — top of the draw
    // deck instead of the discard pile. It touches no country and no unit.

    /// <summary>
    /// The Build Army card this activation is about, or null when there is none.
    ///
    /// The trigger condition and the step must resolve it the SAME way. Under pool scope the window's
    /// trigger is the deploy event, not the play, so the step can no longer read ActivationTrigger — it
    /// would find no PlayCardChangeEvent and the card would be offered only to no-op. LastOrDefault,
    /// not First: a round can hold two plays by the same faction (StatusGuards plays a Build Army card
    /// from inside its own step), and the most recent is the one just played.
    ///
    /// The discard-pile clause excludes a Status/Response table play, as in ResponseRationing.
    /// </summary>
    private PlayCardChangeEvent BuildArmyCard() =>
        CardPlayPool.ChangeEventsPool
            .OfType<PlayCardChangeEvent>()
            .LastOrDefault(playCard =>
                playCard.TriggeringFaction == Faction
                && CardState.ForId(playCard.SourceCardId)?.CardData.CardType == CardType.BUILD_ARMY
                && DeckState.ForFaction(Faction).DiscardedCardIds.Contains(playCard.SourceCardId));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.FactionPlayedCard(Faction), this),
            Condition.Build(new Condition.IsGameFlowStep(TurnStep.PLAY_CARD), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.CustomCondition(() => BuildArmyCard() != null), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                PlayCardChangeEvent playEvent = BuildArmyCard();
                if (playEvent == null)
                {
                    DebugUtilities.PrintPeerError("Women Conscripts: no Build Army card found to recycle");
                    return;
                }

                ModifierRegistry.Register(new MutatorRecycleAfterStep(
                    Faction,
                    playEvent.SourceCardId,
                    CardState.Id,
                    GameFlow.Instance.TurnStep,
                    RecycleDestination.TopOfDeck,
                    "Women Conscripts: return the Build Army card to the top of the draw deck",
                    "The call-up never stops. The Build Army card just played goes back on top of its owner's draw deck instead of being discarded."));

                await Task.CompletedTask;
                // the mutator does the work once the step ends
            })
            .WithGuidance("Place Build Army card on top of your draw deck at the end of this step")
        };
    }
}
