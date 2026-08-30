using System.Collections.Generic;
using System.Threading.Tasks;

public class ShowDiscardModalAnimation : ShowCardsModalAnimation
{
    private readonly Faction _targetFaction;

    public override List<Faction> ForFactions => new List<Faction>{_targetFaction};

    /// <summary>
    /// The target faction is passed in rather than read off the first discarded card: a discard can
    /// legitimately resolve to zero cards (empty deck, or a modifier reducing the count to 0), and
    /// deriving it from _cardIds[0] threw ArgumentOutOfRangeException in that case.
    /// </summary>
    public ShowDiscardModalAnimation(List<int> cardIds, string title, Faction targetFaction) : base(cardIds, title)
    {
        _targetFaction = targetFaction;
    }

    protected override async Task AnimateForTargetFaction()
    {
        if (_cardIds.Count == 0)
            return;

        List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(_cardIds, false);
        // Burns rather than only auto-dismissing: the cards are gone from the hand, so they leave the
        // screen the same way. The burn is also what times the modal now - it closes when nothing is left.
        await ModalStack.Current.Show(ModalConfig.Display(_title, presentationItems).WithBurnAway());
    }

    protected override async Task AnimateForEnemyFaction()
    {
        if (_cardIds.Count == 0)
            return;

        DebugUtilities.PrintPeer($"AnimateForEnemyFaction: {_targetFaction}");
        FactionsContainer.Current.FactionInfoNodes[_targetFaction].ShowCardDelta(_cardIds.Count * -1);

    }
}
