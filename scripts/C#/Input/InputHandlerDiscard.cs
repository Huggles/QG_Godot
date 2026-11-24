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
        // A sorted copy, matching the hand fan's card-id order; the caller may pass the live hand list.
        this.cardIds = cardIds.OrderBy(id => id).ToList();
        this.minimumDiscards = minimumDiscards;
        this.required = required;
    }

    public async Task<List<int>> GetSelectedCards()
    {
        // A required discard is always from hand, so it is picked in the hand fan. The optional path
        // offers cards from elsewhere (the discard pile) and keeps the modal.
        if (required)
            return await PickFromHand(faction, cardIds, CardPick.Exactly(minimumDiscards),
                string.Format(TranslationServer.Translate(minimumDiscards == 1 ? "HUD_DISCARD_SELECT_N_ONE" : "HUD_DISCARD_SELECT_N_OTHER"), minimumDiscards));

        string title = TranslationServer.Translate("HUD_DISCARD_SELECT_OPTIONAL");
        PlayerActionLabel.ShowText($"{faction.Label()}: {title}", faction);
        List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(cardIds, true);
        ModalResult result = await ModalStack.Current.Show(ModalConfig.SelectMany(title, presentationItems, minimumDiscards));
        return result.WasCancelled ? new List<int>() : result.SelectedItems;
    }

    /// <summary>
    /// Picks cards straight in the hand fan: they rise when clicked and the button above the fan
    /// confirms. Released as an empty pick when the host abandons the request, like any card prompt.
    /// </summary>
    public static async Task<List<int>> PickFromHand(Faction faction, List<int> cardIds, CardPick pick, string title)
    {
        InputManager input = InputManager.Current;
        if (input == null || FactionHandDisplay.Current == null) return new List<int>();

        Task<List<int>> picked = input.SetCardPickActive(faction, cardIds, pick, title);
        using (PendingLocalInput.Register(() => InputManager.Current?.CancelCardSelection()))
        {
            return await picked;
        }
    }
}
