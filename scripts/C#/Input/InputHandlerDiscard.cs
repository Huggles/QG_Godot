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
    private List<int> selectedCardIds = new List<int>();
    private TaskCompletionSource<List<int>> completionSource;

    public InputHandlerDiscard(Faction faction, int minimumDiscards, bool required)
    {
        this.faction = faction;
        this.minimumDiscards = minimumDiscards;
        this.required = required;
    }

    public async Task<List<int>> GetSelectedCards()
    {
        completionSource = new TaskCompletionSource<List<int>>();
        
        DeckState deckState = DeckState.ForFaction(faction);
        List<int> handCardIds = deckState.HandCardIds;

        if (handCardIds.Count == 0)
        {
            completionSource.SetResult(new List<int>());
            return await completionSource.Task;
        }

        string title = required 
            ? $"Select at least {minimumDiscards} card(s) to discard"
            : "Select card(s) to discard (or skip)";

        PlayerActionLabel.ShowText(title, faction);
        
        // Create presentation items for each card in hand
        List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(handCardIds, true);

        // Show modal with multi-select enabled
        PresentationModal.Instance.ShowModalMultiSelect(
            presentationItems, 
            title, 
            minimumDiscards,
            HandleConfirm,
            HandleSkip
        );

        return await completionSource.Task;
    }

    private void HandleConfirm(List<int> selectedIdentifiers)
    {
        selectedCardIds = selectedIdentifiers;
        PresentationModal.Instance.HideModal();
        completionSource?.SetResult(selectedCardIds);
    }

    private void HandleSkip()
    {
        if (!required || minimumDiscards == 0)
        {
            selectedCardIds.Clear();
            PresentationModal.Instance.HideModal();
            completionSource?.SetResult(selectedCardIds);
        }
    }
}
