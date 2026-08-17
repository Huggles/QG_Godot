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
            ? $"Select {minimumDiscards} card(s) to discard"
            : "Select card(s) to discard (or skip)";

        PlayerActionLabel.ShowText(title, faction);

        // Create presentation items for each card in hand
        List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(cardIds, true);

        // A required discard is for exactly this many cards — SelectMany leaves MaxSelections
        // unlimited, which let a player discard more than they were asked for. The optional
        // end-of-turn path keeps SelectMany: there the count really is open-ended.
        ModalConfig config = required
            ? ModalConfig.SelectExactly(title, presentationItems, minimumDiscards)
            : ModalConfig.SelectMany(title, presentationItems, minimumDiscards);

        ModalResult result = await ModalStack.Current.Show(config);
        return result.WasCancelled ? new List<int>() : result.SelectedItems;
    }
}
