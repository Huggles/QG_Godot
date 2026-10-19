using System.Collections.Generic;

/// <summary>
/// "Use when Germany plays an Economic Warfare card with the word "Submarines" in its title. The Allied country
/// targeted by the card must discard 3 additional cards from the top of its draw deck." Offered after that
/// card's discard lands, as a discard of its own: the EW card itself is left as it is.
/// </summary>
public partial class BolsterBETASOM : BolsterCardLogic
{
    /// <summary>The Allied country a German "Submarines" EW card just made discard, or NONE.</summary>
    private Faction Target(GameSituation situation) =>
        TriggerContextIn(situation) is (ForceDiscardCardsChangeEvent or ForceDiscardHandCardsChangeEvent) and var discard
        && discard.TriggeringFaction == Faction.GERMANY
        && discard.SourceCardState?.CardData is { CardType: CardType.ECONOMIC_WARFARE } source
        && source.Label.Contains("Submarines")
        && StaticGameData.FactionTeamForFaction(discard.TargetFaction) == FactionTeam.ALLIES
            ? discard.TargetFaction
            : Faction.NONE;

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.CustomCondition(s => Target(s) != Faction.NONE).InReactionWindow(), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c => new ForceDiscardCardsChangeEvent(Faction, Target(c.Situation), 3)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => Target(s) != Faction.NONE), this))
            .WithGuidance("The Allied country discards 3 more cards from the top of its draw deck")
    };
}
