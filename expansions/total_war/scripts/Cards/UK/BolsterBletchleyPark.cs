using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// "Use when the Axis declares its intention to use a Bolster card, but before any text is resolved. Discard 1 card
/// from hand to discard the Axis Bolster card without resolving its text." Offered in the block window of that
/// Bolster's first effect; BlockCard() ends the card before its price is paid, and it already lies in its owner's
/// discard pile (UseBolsterChangeEvent). A Bolster whose steps open no window (Code of Bushido) cannot be answered.
/// </summary>
public partial class BolsterBletchleyPark : BolsterCardLogic
{
    /// <summary>The Axis Bolster being cancelled: a Card target, which lights nothing on the map.</summary>
    public override TargetSet Targets()
    {
        int sourceCardId = BlockContext?.SourceCardId ?? -1;
        return sourceCardId < 0 ? TargetSet.None : TargetSet.Cards(new List<int> { sourceCardId });
    }

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsBlockRequestFromTeam(FactionTeam.AXIS, CardType.BOLSTER), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new BlockStep<ChangeEvent>(this, async blocked => {
            await new ShowActionLabelPresentationEvent(Faction,
                $"{Faction.WithPlayer()} uses Bletchley Park: {blocked.SourceCardState?.CardName} is discarded without effect").Apply();
            await Task.Delay(GameSettings.DurationMedium);
            return CardStepResult.BlockCard();
        })
        .WithPrice(TotalWarCards.HandDiscardPrice(this, 1))
        .WithGuidance("Discard 1 card to discard the Axis Bolster card without resolving it")
    };
}
