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
    private const string IconDraw            = "res://assets/textures/Other/Icons/PlayedCardsIcon.png";
    // Same asset as IconDraw, named separately so either mapping can move without disturbing the other.
    private const string IconReorder         = "res://assets/textures/Other/Icons/PlayedCardsIcon.png";
    private const string IconPlayCard        = "res://assets/textures/Other/Icons/PlayCardIcon.png";
    private const string IconDiscard         = "res://assets/textures/Other/Icons/DiscardIcon.png";
    private const string IconDiscardFromHand = "res://assets/textures/Other/Icons/DiscardFromHandIcon.png";
    private const string IconScorePoints     = "res://assets/textures/Other/Icons/ScorePointsIcon.png";
    private const string IconArmy            = "res://assets/textures/Units/QGArmyDetailed.png";
    private const string IconNavy            = "res://assets/textures/Units/QGNavyDetailed.png";

    // Badge flags, which replace the faction flag rather than sitting over it. See HistoryFlagPath.
    private const string FlagNextRound        = "res://assets/textures/Other/NextRoundFlag.png";

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

        PlayCardChangeEvent                   => IconPlayCard,

        DrawCardsChangeEvent                  => IconDraw,
        DrawCardByNameChangeEvent             => IconDraw,

        // Split on where the cards come from, which is what the two icons depict.
        // ForceDiscardCardsChangeEvent discards off the top of the draw deck (DiscardTopCards); the
        // other two take them out of the target's hand.
        ForceDiscardCardsChangeEvent          => IconDiscard,
        DiscardHandCardsChangeEvent           => IconDiscardFromHand,
        ForceDiscardHandCardsChangeEvent      => IconDiscardFromHand,

        ReorderDeckChangeEvent                => IconReorder,

        ScorePointsChangeEvent                => IconScorePoints,

        _                                     => null,
    };

    /// <summary>
    /// A flag texture that replaces the acting faction's on the badge, or null to use the faction's own.
    ///
    /// For an entry that belongs to no faction but still has to be recognisable at a glance. A new
    /// round is the whole table's event — Faction.NONE, so it would otherwise be an anonymous grey
    /// badge — and it flies the composite all-factions flag instead. A badge with an override flag
    /// carries no icon and no faction colour wash: the flag *is* the picture.
    /// </summary>
    public static string HistoryFlagPath(this GameMessage message) => message switch
    {
        ChangeRoundChangeEvent                => FlagNextRound,
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
    /// What caused this event, named: "Build Army", "Blitzkrieg (Status card)", "a hidden Response
    /// card". Null for an event with no source card — the turn structure's own draws and step changes
    /// — which is the display's cue to leave the line off entirely.
    ///
    /// This is the half of the trigger context the summary cannot carry. "Germany deployed to India"
    /// is true of a Build Army card and of a Status card firing alike, and the reaction window's card
    /// image is easy to misread as an offer rather than as the cause, so the cause gets said in words.
    /// The <c>Cause:</c> prefix is <see cref="TriggerContextDisplay"/>'s, not this fragment's.
    ///
    /// The type is spelled out only for STATUS/RESPONSE/EVENT: a Build Army card is already called
    /// "Build Army", and "Build Army (Build Army card)" says nothing twice.
    ///
    /// A Response card that has not been revealed is redacted rather than named, the same split
    /// <see cref="PlayCardChangeEvent.SummaryText"/> makes — this text goes to the reacting player
    /// over the wire, and naming a face-down card there would hand them the one thing the
    /// always-ask reaction window exists to hide.
    ///
    /// Must never throw: it rides <c>InputRequest.TriggerCauseText</c> beside SummaryText(), so a
    /// lookup that threw here would cost a turn rather than a line of text.
    /// </summary>
    public static string CauseText(this ChangeEvent changeEvent)
    {
        // GameSession first, as in DeployIcon: CardState.ForId dereferences GameSession.Current
        // without a guard of its own, so it NREs rather than answering null outside a game.
        bool hasSourceCard = GameSession.Current != null && changeEvent?.SourceCardId > -1;
        CardState card = hasSourceCard ? CardState.ForId(changeEvent.SourceCardId) : null;
        CardData data = card?.CardData;
        if (data == null) return null;

        if (data.CardType == CardType.RESPONSE && !card.IsRevealed)
            return "a hidden Response card";

        string name = card.CardName;
        if (string.IsNullOrEmpty(name)) return null;

        return data.CardType switch
        {
            CardType.STATUS or CardType.RESPONSE or CardType.EVENT
                => $"{name} ({data.CardType.Label()} card)",
            _   => name,
        };
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
