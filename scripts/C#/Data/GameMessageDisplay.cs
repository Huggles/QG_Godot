using System.Collections.Generic;

/// <summary>
/// Display data for a <see cref="GameMessage"/> that is not the message's own SummaryText() — what
/// the game history strip needs to draw one badge. Same doctrine as <see cref="FactionDisplay"/>:
/// one place owns what a player reads.
/// </summary>
public static class GameMessageDisplay
{
    // The badge icons. Paths rather than Texture2D so nothing here loads a texture on a headless
    // server — GameHistoryItem resolves them, and it only ever runs with a UI.
    private const string IconCards = "res://assets/textures/Other/PlayedCardsIcon.png";
    private const string IconDeck  = "res://assets/textures/Other/DeckIcon.png";
    private const string IconArmy  = "res://assets/textures/Units/QGArmyDetailed.png";
    private const string IconNavy  = "res://assets/textures/Units/QGNavyDetailed.png";

    /// <summary>
    /// The icon drawn over the faction flag on a history badge, or null for a message that has no
    /// icon yet — those badges show the flag and the sequence number alone.
    ///
    /// ORDER IS LOAD-BEARING. BattleUnitChangeEvent derives from RemoveUnitChangeEvent which derives
    /// from BattleCountryChangeEvent, so the derived patterns must come first — the same trap
    /// <see cref="BattleCountryChangeEvent.IsBattle"/> documents.
    /// </summary>
    public static string HistoryIconPath(this GameMessage message) => message switch
    {
        DeployUnitChangeEvent d               => DeployIcon(d),

        DrawCardsChangeEvent                  => IconCards,
        DrawCardByNameChangeEvent             => IconCards,

        DiscardHandCardsChangeEvent           => IconDeck,
        ForceDiscardCardsChangeEvent          => IconDeck,
        ForceDiscardHandCardsChangeEvent      => IconDeck,

        _                                     => null,
    };

    /// <summary>
    /// Whose badge this is — the faction whose flag the entry flies.
    ///
    /// Not simply TriggeringFaction, which is Faction.NONE for anything the turn structure does on a
    /// player's behalf: an automatic draw at the start of the draw step is constructed as
    /// <c>new DrawCardsChangeEvent(Faction.NONE, faction, ...)</c>, so keying the flag off the
    /// trigger left every one of those badges blank. Each case below picks the faction its own
    /// SummaryText() puts first, so the badge and the popup text agree about whose entry it is.
    ///
    /// ORDER IS LOAD-BEARING, as above.
    /// </summary>
    public static Faction HistoryFaction(this GameMessage message)
    {
        Faction faction = message switch
        {
            // The attacker acted; the defender merely owned the unit.
            BattleUnitChangeEvent b               => b.TriggeringFaction,
            // An elimination or supply attrition is about who lost the unit — which is also the
            // faction this event's SummaryText() names.
            RemoveUnitChangeEvent r               => UnitFactionOr(r.UnitId, r.TriggeringFaction),

            // Cards moved in the target's hand or deck.
            DrawCardsChangeEvent d                => d.TargetFaction,
            DrawCardByNameChangeEvent d           => d.TargetFaction,
            DiscardHandCardsChangeEvent d         => d.TargetFaction,
            ForceDiscardCardsChangeEvent d        => d.TargetFaction,
            ForceDiscardHandCardsChangeEvent d    => d.TargetFaction,
            ReorderDeckChangeEvent d              => d.TargetFaction,

            _                                     => message.TriggeringFaction,
        };

        // Last resort for a message that has no acting faction but does name a target — better a
        // target's flag than none at all. NONE/ALL have no flag, so a badge that lands here stays
        // grey, which is correct for a genuinely faction-less entry like a turn step change.
        if (faction == Faction.NONE || faction == Faction.ALL) return message.TargetFaction;
        return faction;
    }

    /// <summary>
    /// The unit's owner, or <paramref name="fallback"/> if it cannot be resolved. Not UnitState.ForId,
    /// which indexes UnitStatesById directly and so throws KeyNotFoundException — unusable on a
    /// display path that must never throw.
    /// </summary>
    private static Faction UnitFactionOr(int unitId, Faction fallback)
    {
        UnitState unit = GameSession.Current?.GameState.UnitStatesById.GetValueOrDefault(unitId);
        return unit?.Faction ?? fallback;
    }

    /// <summary>
    /// DeployUnitChangeEvent.UnitType reads CountryState.ForId(CountryId).Type, so an unresolvable
    /// country would throw. Fall back to no icon rather than crashing while drawing the history.
    /// </summary>
    private static string DeployIcon(DeployUnitChangeEvent deploy)
    {
        // GameSession first: CountryState.ForId dereferences GameSession.Current without a guard, so
        // checking the country alone would NRE before the null test could help.
        if (GameSession.Current == null || CountryState.ForId(deploy.CountryId) == null) return null;
        return deploy.UnitType == UnitType.NAVY ? IconNavy : IconArmy;
    }
}
