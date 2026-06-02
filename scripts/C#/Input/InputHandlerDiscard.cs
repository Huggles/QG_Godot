using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class InputHandlerDiscard
{
    private Faction faction;
    private int minimumDiscards;
    private bool required;

    private List<int> cardIds = new List<int>();

    public InputHandlerDiscard(Faction faction, List<int> cardIds, int minimumDiscards = 1, bool required = false)
    {
        this.faction = faction;
        this.cardIds = cardIds;
        this.minimumDiscards = minimumDiscards;
        this.required = required;
    }

    public async Task<List<int>> GetSelectedCards()
    {
        string title = required 
            ? $"Select at least {minimumDiscards} card(s) to discard"
            : "Select card(s) to discard (or skip)";

        PlayerActionLabel.ShowText(title, faction);
        
        // Create presentation items for each card in hand
        List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(cardIds, true);

        // Show modal with multi-select enabled
        Variant[] response = await PresentationModal.Current.ShowModalMultiSelect(
            presentationItems, 
            title, 
            minimumDiscards
        );
        List<int> selectedCardIds = response[0].As<PresentationModal.PresentationItemResponse>().SelectedItems;
        return selectedCardIds;
    }
}
