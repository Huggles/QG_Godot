using System.Collections.Generic;
using System.Threading.Tasks;

public class ShowDiscardModalAnimation : ShowCardsModalAnimation
{
    private Faction targetFaction => CardState.ForId(_cardIds[0]).Faction;

    public override List<Faction> ForFactions => new List<Faction>{targetFaction};

    public ShowDiscardModalAnimation(List<int> cardIds, string title) : base(cardIds, title) {}

    protected override async Task AnimateForTargetFaction()
    {        
        List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(_cardIds, false);
        await PresentationModal.Current.ShowModal(presentationItems, _title);        
    }

    protected override async Task AnimateForEnemyFaction()
    {
        DebugUtilities.PrintPeer($"AnimateForEnemyFaction: {targetFaction}");
        FactionsContainer.Current.FactionInfoNodes[targetFaction].ShowCardDelta(_cardIds.Count * -1);

    }
}
