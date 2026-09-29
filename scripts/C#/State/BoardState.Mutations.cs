using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The writes a ChangeEvent makes, as operations on a board. <see cref="ChangeEvent.Mutate"/> calls
/// these, so the live game and a fork change by the same code; everything a board does not hold —
/// signals, animations, notifications — is the event's live-only follow-up, not this file's business.
///
/// Rule refusals throw <see cref="GameAPI.GameAPIException"/> BEFORE anything is written, as they
/// always did: ErrorReporter grades a refusal recoverable on exactly that promise.
/// </summary>
public sealed partial class BoardState
{
    // ── Units ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Put a unit of <paramref name="unitType"/> into a country and return its id. "Build that army
    /// again": a faction already there redeploys the SAME piece, so no pool piece is consumed and the
    /// board does not change. Otherwise the first undeployed piece in creation order is used, which is
    /// what lets every peer agree without the id on the wire.
    /// </summary>
    public int DeployUnit(Faction faction, int countryId, UnitType unitType, DeployType deployType)
    {
        if (IsLive)
            DebugUtilities.PrintPeer($"Deploying unit of type {unitType} for faction {faction} to country {countryId} with deploy type {deployType}");
        CountryState countryState = CountryState.ForId(countryId);
        Dictionary<Faction, int> units = UnitsIn(countryState);

        bool rebuildInPlace = units.ContainsKey(faction);
        TagContainer tags = Of(countryState).Tags;
        bool deployable = deployType == DeployType.BUILD ? tags.Has(Tag.Buildable, faction) : tags.Has(Tag.Recruitable, faction);

        // Fullness cannot block a rebuild in place: the slot being filled is the faction's own. The two
        // reasons stay distinct — a full country and a target that stopped being legal have different
        // causes, and conflating them sends a reader of the log looking in the wrong place.
        if ((units.Count == 3 && !rebuildInPlace) || !deployable)
        {
            string reason = !deployable
                ? $"Country is not {(deployType == DeployType.BUILD ? "buildable" : "recruitable")} for {faction}"
                : $"Country is full ({units.Count}/3 factions: {string.Join(", ", units.Keys)})";
            string exceptionMessage = $"Cannot {deployType} unit of type {unitType} for faction {faction} to country {countryState.StaticCountryData.Label}. {reason}.";
            if (IsLive) DebugUtilities.PrintPeer(exceptionMessage);
            throw new GameAPI.GameAPIException(exceptionMessage);
        }

        // Throws GameRuleException on an empty pool, also before any write.
        int unitId = rebuildInPlace ? units[faction] : UnitPool.GetAvailableUnitForFaction(faction, unitType, this);
        units[faction] = unitId;
        ForUnit(unitId).CountryId = countryId;
        return unitId;
    }

    /// <summary>
    /// Take a unit off the board, back to its pool. The reasons that must not remove a unit — in supply
    /// for a SUPPLY removal, immune or out of reach for a BATTLE — are checked on this board first.
    /// </summary>
    public void RemoveUnit(int unitId, UnitRemovalReason reason, Faction removingFaction)
    {
        UnitState unitState = UnitState.ForId(unitId);

        if (!IsDeployed(unitState))
            throw new GameAPI.GameAPIException(
                $"Cannot remove {unitState.Faction} {unitState.Type} (unit {unitId}) for {reason}: it is not on the board.");

        CountryState countryState = CountryStateOf(unitState);

        switch (reason)
        {
            case UnitRemovalReason.SUPPLY when InSupply(unitState):
                throw new GameAPI.GameAPIException(
                    $"Cannot remove {unitState.Faction} {unitState.Type} in {countryState.Label} for SUPPLY: the unit is in supply.");

            // Not ELIMINATE: whether immunity stops an elimination is an open rules question.
            case UnitRemovalReason.BATTLE when ImmuneForTurn(unitState):
                throw new GameAPI.GameAPIException(
                    $"Cannot remove {unitState.Faction} {unitState.Type} in {countryState.Label} for BATTLE: the unit is immune this turn.");

            // Tag.Attackable means removingFaction has a supplied unit that can reach this target.
            case UnitRemovalReason.BATTLE when !Of(unitState).Tags.Has(Tag.Attackable, removingFaction):
                throw new GameAPI.GameAPIException(
                    $"Cannot remove {unitState.Faction} {unitState.Type} in {countryState.Label} for BATTLE: {removingFaction} has no supplied unit able to attack it.");
        }

        Of(unitState).CountryId = -1;
        UnitsIn(countryState).Remove(unitState.Faction);
    }

    public void AddScore(Faction faction, int points) => ForFaction(faction).Score += points;

    // ── Flow ────────────────────────────────────────────────────────────────

    /// <summary>Count one spent play for the faction this turn step.</summary>
    public void CountPlay(Faction faction)
    {
        Dictionary<Faction, int> played = CardsPlayedThisTurnStep;
        if (!played.ContainsKey(faction)) played[faction] = 1;
        else played[faction] += 1;
    }

    // ── Card piles ──────────────────────────────────────────────────────────
    // A card id must sit in exactly one of a faction's five piles; every move takes it out first.

    public int DrawTopCard(Faction faction)
    {
        FactionRecord piles = ForFaction(faction);
        if (piles.Deck.Count == 0)
        {
            if (IsLive) DebugUtilities.PrintPeerError($"Deck is empty for: {faction.Label()}");
            return -1;
        }

        int topCardId = piles.Deck[0];
        piles.Deck.RemoveAt(0);
        piles.Hand.Add(topCardId);
        return topCardId;
    }

    public List<int> DrawCards(Faction faction, int number)
    {
        var drawn = new List<int>();
        for (int i = 0; i < number; i++)
        {
            int cardId = DrawTopCard(faction);
            if (cardId > 0)
                drawn.Add(cardId);
        }
        return drawn;
    }

    /// <summary>The top cards of the deck to the discard pile; fewer when the deck runs short.</summary>
    public List<int> DiscardTopCards(Faction faction, int number)
    {
        FactionRecord piles = ForFaction(faction);
        int overdraw = Math.Max(number - piles.Deck.Count, 0);
        if (overdraw > 0 && IsLive)
            DebugUtilities.PrintPeerError($"Deck has only {piles.Deck.Count} of {number} cards to discard for: {faction.Label()}");

        List<int> discarded = new List<int>();
        for (int i = 0; i < number - overdraw; i++)
        {
            discarded.Add(piles.Deck[0]);
            piles.Deck.RemoveAt(0);
        }
        piles.Discarded.AddRange(discarded);
        return discarded;
    }

    /// <summary>
    /// A named card from the draw deck to hand, wherever it sits. Searched over the deck's ids, in
    /// deck order: CardState.ForIds would answer in id order, and the deck is shuffled.
    /// </summary>
    public int DrawCardByName(Faction faction, string cardName)
    {
        FactionRecord piles = ForFaction(faction);
        int index = piles.Deck.FindIndex(id => CardState.ForId(id)?.CardData.UniqueName == cardName);
        if (index == -1)
            return -1;

        int cardId = piles.Deck[index];
        piles.Deck.RemoveAt(index);
        piles.Hand.Add(cardId);
        return cardId;
    }

    /// <summary>
    /// A copy of the named card the faction could still discard — any pile but the discard pile, so a
    /// repeated name finds the NEXT copy — or -1. Scoped to one faction: generic cards appear in
    /// several decks, and CardState.ForName answers with whichever copy it happens to hold.
    /// </summary>
    public int FindDiscardableCardByName(Faction faction, string cardName)
    {
        FactionRecord piles = ForFaction(faction);
        List<int> searchable = piles.Deck.Concat(piles.Hand).Concat(piles.Response).Concat(piles.Status).ToList();
        int index = searchable.FindIndex(id => CardState.ForId(id)?.CardData.UniqueName == cardName);
        return index == -1 ? -1 : searchable[index];
    }

    public int DiscardCardByName(Faction faction, string cardName)
    {
        int cardId = FindDiscardableCardByName(faction, cardName);
        if (cardId == -1)
            return -1;

        DiscardCard(faction, cardId);
        return cardId;
    }

    public void DiscardHandCards(Faction faction, List<int> cardIds)
    {
        foreach (int cardId in cardIds)
            DiscardCard(faction, cardId);
    }

    /// <summary>
    /// A card from hand or deck onto the table: a Status card registers its modifier on this board, a
    /// Response card goes face down, anything else is spent to the discard pile.
    /// </summary>
    public void PlayCard(Faction faction, int cardId)
    {
        FactionRecord piles = ForFaction(faction);
        if (!piles.Hand.Contains(cardId) && !piles.Deck.Contains(cardId))
        {
            if (IsLive) DebugUtilities.PrintPeerError($"Cannot play card that is not in hand or deck: {cardId}");
            return;
        }
        piles.Hand.Remove(cardId);
        piles.Deck.Remove(cardId);

        CardState cardState = CardState.ForId(cardId);
        if (cardState.CardData.CardType == CardType.STATUS)
        {
            piles.Status.Add(cardId);
            if (cardState.CardLogic is IModifier modifier)
                RegisterModifier(modifier);
        }
        else if (cardState.CardData.CardType == CardType.RESPONSE)
        {
            Of(cardState).IsRevealed = false;
            piles.Response.Add(cardId);
        }
        else
            DiscardCard(faction, cardId);
    }

    public void DiscardCard(Faction faction, int cardId)
    {
        RemoveCardFromAnyPile(faction, cardId);
        ForFaction(faction).Discarded.Add(cardId);
    }

    /// <summary>
    /// Take a card out of every pile it could be in, and say whether any held it. A Status card
    /// leaving the table takes its modifier with it, unless the modifier is meant to outlive it.
    /// </summary>
    public bool RemoveCardFromAnyPile(Faction faction, int cardId)
    {
        FactionRecord piles = ForFaction(faction);
        // Non-short-circuiting `|`: every pile gets cleared, not just the first one that matches.
        bool removed = piles.Hand.Remove(cardId)
                     | piles.Deck.Remove(cardId)
                     | piles.Discarded.Remove(cardId)
                     | piles.Response.Remove(cardId);

        if (piles.Status.Remove(cardId))
        {
            removed = true;
            if (CardState.ForId(cardId).CardLogic is IModifier modifier and not IPersistentModifier)
                UnregisterModifier(modifier);
        }
        return removed;
    }

    /// <summary>
    /// Shuffle the draw deck and return the order. With <paramref name="order"/> the deck takes that
    /// order verbatim — the host's shuffle on a client, or a recorded one on replay. A fork never
    /// draws from the game's RNG: without an order it keeps the deck as it is.
    /// </summary>
    public List<int> ShuffleDeck(Faction faction, List<int> order = null)
    {
        List<int> deck = ForFaction(faction).Deck;
        if (order != null)
        {
            // Mutated in place, never reassigned: pile lists are live objects other code holds.
            deck.Clear();
            deck.AddRange(order);
        }
        else if (IsLive)
        {
            GameRandom.Shuffle(deck);
        }
        return new List<int>(deck);
    }
}
