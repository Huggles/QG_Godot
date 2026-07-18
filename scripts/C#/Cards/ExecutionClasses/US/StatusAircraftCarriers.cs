using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusAircraftCarriers : StatusCardLogic
{
    private BattleCountryChangeEvent LastSeaBattle =>
        CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
            .LastOrDefault(ce => ce.TriggeringFaction == Faction && ce.CountryState.Type == CountryType.SEA);

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasBattledAtSea(Faction), this),
            Condition.Build(new Condition.CardHasNotBeenActivatedThisTurn(CardState), this),
            Condition.Build(new Condition.CustomCondition(() => {
                var battle = LastSeaBattle;
                return battle != null && CountryState.ForId(battle.CountryId).Tags.Has(Tag.Buildable, Faction);
            }), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 1));
                discardEvent.IsTrigger = false;
                await discardEvent.ApplyChange();

                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(discardEvent.DiscardedCardIds, false);
                await PresentationModal.Current.ShowModal(presentationItems, "Discarded cards");

                int countryId = LastSeaBattle.CountryId;
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => {
                var battle = LastSeaBattle;
                return battle != null && CountryState.ForId(battle.CountryId).Tags.Has(Tag.Buildable, Faction);
            }), this))
            .WithGuidance("Discard top 1 deck card to build a Navy in the sea space just battled")
        };
    }
}