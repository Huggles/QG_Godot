using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// "Use when the Axis declares its intention to use a Status card, but before any text is resolved. Discard 1 card
/// from your hand to prevent the Axis from using that Status card this turn." Offered in the block window of that
/// Status card's first effect; BlockCard() ends the activation before its price is paid, and CardState.IsBlocked
/// keeps it unusable until ChangeRoundChangeEvent clears it. A Status card whose steps open no window cannot be answered.
/// </summary>
public partial class BolsterKeepCalmandCarryOn : BolsterCardLogic
{
    /// <summary>The Axis Status card being stopped: a Card target, which lights nothing on the map.</summary>
    public override TargetSet Targets()
    {
        int sourceCardId = BlockContext?.SourceCardId ?? -1;
        return sourceCardId < 0 ? TargetSet.None : TargetSet.Cards(new List<int> { sourceCardId });
    }

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsBlockRequestFromTeam(FactionTeam.AXIS, CardType.STATUS), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new BlockStep<ChangeEvent>(this, async blocked => {
            await new ShowActionLabelPresentationEvent(Faction,
                $"{Faction.WithPlayer()} uses Keep Calm and Carry On: {blocked.SourceCardState?.CardName} cannot be used this turn").Apply();
            await Task.Delay(GameSettings.DurationMedium);
            return CardStepResult.BlockCard();
        })
        .WithPrice(TotalWarCards.HandDiscardPrice(this, 1))
        .WithGuidance("Discard 1 card to stop the Axis Status card this turn")
    };
}
