using System.Linq;

/// <summary>
/// The board facts the battle rules share, in one place so the card-level rule and the target-level
/// rule cannot come to different conclusions about the same board.
///
/// That matters more than it sounds: <see cref="AvoidDeadBattleCardRule"/> decides whether to play a
/// battle card, and <see cref="AvoidEmptyBattleRule"/> decides what to hit once it is played. If the
/// first says "there is nothing to attack, don't bother" and the second says "there is something to
/// attack" the bot spends the card and then wastes it, which is worse than either rule alone.
/// </summary>
public static class BotBattleFacts
{
    /// <summary>
    /// Whether the faction has an enemy unit it could actually fight with a battle card of this type.
    ///
    /// The same accessors the battle cards themselves target with — LandBattle reads
    /// UnitState.AttackableArmyIds, SeaBattle the navy equivalent — so this asks the card's own
    /// question rather than a paraphrase of it.
    /// </summary>
    public static bool HasRealTarget(CardType cardType, Faction faction) => cardType switch
    {
        CardType.LAND_BATTLE => UnitState.AttackableArmies(faction).Any(),
        CardType.SEA_BATTLE => UnitState.AttackableNavies(faction).Any(),
        _ => true,   // not a battle card: this rule has no opinion
    };

    /// <summary>Whether a card of this type resolves into a battle at all.</summary>
    public static bool IsBattleCard(CardType cardType)
        => cardType is CardType.LAND_BATTLE or CardType.SEA_BATTLE;

    /// <summary>
    /// Whether the faction has anything on the table that a battle might trigger — the ONLY reason to
    /// attack an empty space, since the attack itself changes nothing on the board.
    ///
    /// DeckState's StatusCardIds and ResponseCardIds are the in-play table piles specifically:
    /// DeckState.PlayCard moves a Status card there when it is played and a Response card there face
    /// down, and DiscardCard pulls a spent one back out. So a non-empty pile means "this faction has a
    /// card sitting on the table that is still waiting for something to happen".
    ///
    /// Deliberately coarse — it does not check whether those cards' triggers involve a BATTLE. Reading
    /// a card's triggers would mean either exposing CardLogic.CardTriggers() or pattern-matching the
    /// Condition subclasses that mean "battled" (HasBattledOnLand, HasBattledAtSea, FactionBattled,
    /// and whatever a future card adds), and getting that list wrong fails silently in the direction of
    /// never trying for a trigger at all.
    ///
    /// Also deliberately the faction's own table, not the team's: a teammate's Response could trigger
    /// off this battle too, so this under-counts. Widening it to the team is a one-line change if the
    /// measured trigger rate turns out to be worth it.
    ///
    /// Being coarse is why both callers treat a true answer as a REASON TO PREFER SOMETHING ELSE
    /// rather than as permission: "there is a card on the table that might want a battle" is a much
    /// weaker statement than "this battle will fire it".
    /// </summary>
    public static bool HasTriggerableTableCards(Faction faction)
    {
        DeckState deck = DeckState.ForFaction(faction);
        return deck.StatusCardIds.Count > 0 || deck.ResponseCardIds.Count > 0;
    }
}
