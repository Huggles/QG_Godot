using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class InputHandlerDiscardHand : Node
{
    private Faction faction;

    public InputHandlerDiscardHand(Faction faction)
    {
        this.faction = faction;
    }

    public async Task<List<int>> GetSelectedCards()
    {
        DeckState deckState = DeckState.ForFaction(faction);
        List<int> handCardIds = deckState.HandCardIds;
        handCardIds.Sort();

        string title = "Select card(s) to discard (or skip)";

        PlayerActionLabel.ShowText(title, faction);
        
        // Create presentation items for each card in hand
        List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(handCardIds, true);

        // Show modal with multi-select enabled
        ModalResult result = await ModalStack.Current.Show(
            ModalConfig.SelectMany(title, presentationItems, 0));
        return result.WasCancelled ? new List<int>() : result.SelectedItems;
    }
}
