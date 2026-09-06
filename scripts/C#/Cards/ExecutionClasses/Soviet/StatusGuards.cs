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

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                SpendPlayActionChangeEvent spendEvent = BuildChangeEvent(new SpendPlayActionChangeEvent(Faction));
                spendEvent.IsTrigger = false;
                await spendEvent.Apply();

                ForceDiscardHandCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardHandCardsChangeEvent(Faction, Faction, 2));
                discardEvent.IsTrigger = false;
                await discardEvent.Apply();

                // Play a BuildArmy card from discard so that reaction cards (e.g. Women Conscripts)
                // trigger correctly on the resulting PlayCardChangeEvent.
                int buildArmyCardId = DeckState.ForFaction(Faction).DiscardedCardIds
                    .First(id => CardState.ForId(id).CardData.CardType == CardType.BUILD_ARMY);
                RecycleCardChangeEvent recycleEvent = BuildChangeEvent(
                    new RecycleCardChangeEvent(Faction, Faction, buildArmyCardId, RecycleDestination.Hand));
                recycleEvent.IsTrigger = false;
                await recycleEvent.Apply();
                await CardPlayPool.DoCard(buildArmyCardId);
            })
            .WithGuidance("Discard 2 cards from hand to play a Build Army card from your discard pile")
            .WithCondition(() => Condition.Build(new Condition.HasBuildableLand(Faction), this))
        };
    }
}