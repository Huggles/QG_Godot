using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusGuards : StatusCardLogic
{
    /// <summary>Where the recovered Build Army card could then build. The card it fishes out of the
    /// discard pile is reported too, though a Card target names no board space.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(CountryState.BuildableLand(Faction))
            .Plus(TargetSet.Cards(DeckState.ForFaction(Faction).DiscardedCardIds
                .Where(id => CardState.ForId(id).CardData.CardType == CardType.BUILD_ARMY).ToList()));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this),
            Condition.Build(new Condition.CustomCondition(() =>
                DeckState.ForFaction(Faction).HandCardIds.Count >= 2), this),
            Condition.Build(new Condition.CustomCondition(() =>
                DeckState.ForFaction(Faction).DiscardedCardIds.Any(id =>
                    CardState.ForId(id).CardData.CardType == CardType.BUILD_ARMY)), this),
            Condition.Build(new Condition.HasBuildableLand(Faction), this)
        };
    }

    /// <summary>The first Build Army card in the discard pile — the one the recycle step recovers.</summary>
    private int DiscardedBuildArmyCardId => DeckState.ForFaction(Faction).DiscardedCardIds
        .First(id => CardState.ForId(id).CardData.CardType == CardType.BUILD_ARMY);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // The card's gate lives on the FIRST step, which is also the one that spends the play.
            new RequirementStep(this, Choose.Fixed(() => new SpendPlayActionChangeEvent(Faction)))
            .WithCondition(() => Condition.Build(new Condition.HasBuildableLand(Faction), this))
            .WithGuidance("Discard 2 cards from hand to play a Build Army card from your discard pile"),

            new RequirementStep(this, Choose.Fixed(() => new ForceDiscardHandCardsChangeEvent(Faction, Faction, 2)))
            .RequiringPreviousStep(),

            // Recycled to hand first, then PLAYED from there, so the play emits a real
            // PlayCardChangeEvent — which is what lets reaction cards (Women Conscripts) trigger on it.
            new RequirementStep(this, Choose.Fixed(() => new RecycleCardChangeEvent(Faction, Faction, DiscardedBuildArmyCardId, RecycleDestination.Hand)))
            .RequiringPreviousStep(),

            // Plays exactly the card the recycle step moved to hand, read off that step's outcome.
            new PlayCardStep(this, PlayChoice.Fixed(previous => ((RecycleCardChangeEvent)previous.Value.Event).CardId))
            .RequiringPreviousStep()
        };
    }
}
