using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Reveals a discard to the table. ForFactions is left at its default (every playable faction), so
/// every GUI peer runs <see cref="AnimateForTargetFaction"/> and sees it happen — a discard is public.
/// Faces follow CardState.IsFaceVisibleToLocalPlayer, so an opponent gets card backs for anything
/// that was never revealed.
/// </summary>
public class ShowDiscardModalAnimation : ShowCardsModalAnimation
{
    private readonly Faction _targetFaction;
    private readonly bool _targetChoseCards;

    /// <summary>
    /// The target faction is passed in rather than read off the first discarded card: a discard can
    /// legitimately resolve to zero cards (empty deck, or a modifier reducing the count to 0), and
    /// deriving it from _cardIds[0] threw ArgumentOutOfRangeException in that case.
    ///
    /// <paramref name="targetChoseCards"/> must be derivable from replicated state, because each peer
    /// builds its own copy of this animation from the event's AfterAnimations.
    /// </summary>
    public ShowDiscardModalAnimation(List<int> cardIds, string title, Faction targetFaction, bool targetChoseCards = false) : base(cardIds, title)
    {
        _targetFaction = targetFaction;
        _targetChoseCards = targetChoseCards;
    }

    protected override async Task AnimateForTargetFaction()
    {
        if (_cardIds.Count == 0)
            return;

        FactionsContainer.Current.FactionInfoNodes[_targetFaction].ShowCardDelta(_cardIds.Count * -1);

        // The player who picked these out of their own hand chose them a moment ago in the discard
        // prompt, so replaying them is noise — the badge above is feedback enough. Everyone else is
        // seeing the discard for the first time and gets the modal.
        if (_targetChoseCards && PresentationServices.Notification.LocalPlayerControls(_targetFaction))
            return;

        List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(_cardIds, false);
        // Burns rather than only auto-dismissing: the cards are gone from the hand, so they leave the
        // screen the same way. The burn is also what times the modal now - it closes when nothing is left.
        await ModalStack.Current.Show(ModalConfig.Display(_title, presentationItems).WithBurnAway());
    }
}
