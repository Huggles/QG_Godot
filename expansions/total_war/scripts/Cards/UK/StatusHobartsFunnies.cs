using System.Linq;

/// <summary>
/// "Axis Status cards are ignored during the United Kingdom turn." Passive: an IStatusIgnoreModifier, so
/// during the UK turn BoardState.Modifiers leaves the Axis Status cards' modifiers out (Gibraltar's strait,
/// Turkey's links, their supply and VP effects) and CanBeActivated refuses them (their triggers answer
/// nothing). A new turn recalculates everything, so the effect switches on and off at the turn's edges.
/// </summary>
public partial class StatusHobartsFunnies : StatusCardLogic, IStatusIgnoreModifier
{
    public bool Ignores(BoardState board, CardLogic card) =>
        board.CurrentFaction == Faction.UNITED_KINGDOM
        && card.IsStatus
        && StaticGameData.FactionTeamForFaction(card.Faction) == FactionTeam.AXIS;

    /// <summary>The Axis Status cards on the table, the ones this switches off.</summary>
    public override TargetSet Targets() => TargetSet.Cards(StaticGameData.FactionsForTeam(FactionTeam.AXIS)
        .SelectMany(faction => DeckState.ForFaction(faction).StatusCardIds));
}
