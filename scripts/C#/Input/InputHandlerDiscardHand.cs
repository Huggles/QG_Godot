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

        PlayerActionLabel.ShowText($"{faction.Label()}: {title}", faction);
        
        // Create presentation items for each card in hand
        List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(handCardIds, true);

        // Total War: discarding nothing costs VP, so the confirm button says so while nothing is picked.
        ModalConfig config = ModalConfig.SelectMany(title, presentationItems, 0);
        int penalty = GameOptions.Rules.DiscardNothingPenaltyVP;
        if (penalty > 0)
            config.WithEmptyApplyLabel($"Discard nothing (-{penalty} VP)");
        ModalResult result = await ModalStack.Current.Show(config);
        return result.WasCancelled ? new List<int>() : result.SelectedItems;
    }
}
