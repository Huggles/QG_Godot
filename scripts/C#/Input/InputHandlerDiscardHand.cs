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
        List<int> handCardIds = new List<int>(DeckState.ForFaction(faction).HandCardIds);
        handCardIds.Sort();

        // Total War: discarding nothing costs VP, so the confirm button says so while nothing is picked.
        int penalty = GameOptions.Rules.DiscardNothingPenaltyVP;
        CardPick pick = new CardPick(0, -1, penalty > 0 ? $"Discard nothing (-{penalty} VP)" : null);
        return await InputHandlerDiscard.PickFromHand(faction, handCardIds, pick, "Select cards to discard");
    }
}
