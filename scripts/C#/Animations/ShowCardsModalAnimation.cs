using System.Collections.Generic;
using System.Threading.Tasks;

public class ShowCardsModalAnimation : ChangeEventAnimation
{
    protected readonly List<int> _cardIds;
    protected readonly string _title;

    public ShowCardsModalAnimation(List<int> cardIds, string title)
    {
        _cardIds = cardIds;
        _title = title;
    }

    protected override async Task AnimateForTargetFaction()
    {
        List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(_cardIds, false);
        await PresentationModal.Current.ShowModal(presentationItems, _title);
    }
}
