using System.Collections.Generic;
using System.Linq;

/// <summary>"Use at the beginning of your Draw step. Take 2 Build Army cards from your discard pile and add them to your hand." Takes what there is, up to 2.</summary>
public partial class BolsterAsianReserves : BolsterCardLogic
{
    private List<int> DiscardedBuildArmies(BoardState board) => board.ForFaction(Faction).Discarded
        .Where(cardId => CardState.ForId(cardId).CardData.CardType == CardType.BUILD_ARMY).ToList();

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.DRAW, s => DiscardedBuildArmies(s.Board).Count > 0);

    public override List<CardStep> OnActivate() => new() { TakeStep(), TakeStep() };

    /// <summary>One Build Army card back to hand. They are all alike, so no choice is asked.</summary>
    private CardStep TakeStep() =>
        new ResultStep(this, Choose.Fixed(c => new RecycleCardChangeEvent(Faction, Faction, DiscardedBuildArmies(c.Board)[0], RecycleDestination.Hand)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => DiscardedBuildArmies(s.Board).Count > 0), this))
            .WithGuidance("Take a Build Army card from your discard pile into your hand");
}
