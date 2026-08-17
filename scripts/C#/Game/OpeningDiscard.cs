using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// The mandatory discard every faction makes once, before the first turn starts. Hands are dealt at
/// <see cref="StaticGameData.OpeningHandSize"/> and cut down to <see cref="StaticGameData.HandSize"/>
/// here, so the opening decision is which cards to keep rather than which cards you were given.
///
/// Host-only: called from <c>GameFlow.StartGame</c>, which itself runs behind the IsServer guard in
/// MultiplayerSession. The resulting discards reach clients over the replicated ChangeEvent stream.
///
/// Gather in parallel, apply serially. Every *player* is asked at the same time — the discard has no
/// turn order — but the ChangeEvents are applied one at a time in <see cref="StaticGameData.PlayableFactions"/>
/// order. Applying them concurrently would interleave their broadcasts, GameMessage ids, state hashes
/// and animations, and no two peers would agree on the result.
/// </summary>
public static class OpeningDiscard
{
    public static async Task Run(int numberOfCards)
    {
        if (numberOfCards <= 0) return;

        // Grouped by controlling peer, keeping PlayableFactions order inside each group: one player's
        // factions are asked one after another (a peer is only ever asked one thing at a time, so they
        // must not overlap), while different players are asked at the same time.
        List<IGrouping<int, Faction>> byPeer = StaticGameData.PlayableFactions
            .GroupBy(PlayerFactionRegistry.GetPeerIdForFaction)
            .ToList();

        ConcurrentDictionary<Faction, List<int>> picks = new();

        await Task.WhenAll(byPeer.Select(async peerFactions =>
        {
            foreach (Faction faction in peerFactions)
            {
                int handSize = DeckState.ForFaction(faction).HandCardIds.Count;
                if (handSize == 0) continue;

                try
                {
                    InputRequest response = await new InputRequest
                        .ForceDiscardHandCardsRequestHandler(faction, Math.Min(numberOfCards, handSize))
                        .BroadCast();
                    picks[faction] = response.ResponseCardIds;
                }
                catch (StepSkippedException)
                {
                    // Only reachable through a host timeout / Skip decision — the modal has no Cancel
                    // button while MinSelections > 0. Leave the hand alone rather than stall the start
                    // of the game; that faction simply opens on a larger hand.
                    DebugUtilities.PrintPeerErrorRaw($"Opening discard skipped for {faction}");
                }
            }
        }));

        foreach (Faction faction in StaticGameData.PlayableFactions)
        {
            if (!picks.TryGetValue(faction, out List<int> cardIds) || cardIds.Count == 0) continue;

            ForceDiscardHandCardsChangeEvent discardEvent =
                new(Faction.NONE, faction, cardIds.Count) { IsTrigger = false };
            // Already chosen above, so ExecuteAsync must not raise a second request for it.
            discardEvent.PreselectDiscards(cardIds);
            await discardEvent.Apply();
        }
    }
}
