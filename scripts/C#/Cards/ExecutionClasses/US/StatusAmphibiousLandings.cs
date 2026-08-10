using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusAmphibiousLandings : StatusCardLogic
{
    private BattleCountryChangeEvent LastLandBattle =>
        CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
            .LastOrDefault(ce => ce.IsBattle && ce.TriggeringFaction == Faction && ce.CountryState.Type == CountryType.LAND);

    private bool HasAdjacentSuppliedUSNavy =>
        LastLandBattle != null && CountryState.ForId(LastLandBattle.CountryId).ConnectedCountryStates
            .Any(adj => FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                .Any(us => us.Type == UnitType.NAVY && us.CountryId == adj.Id && us.InSupply));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasBattledOnLand(Faction), this).Immediately(),
            Condition.Build(new Condition.CardHasNotBeenActivatedThisTurn(CardState), this),
            Condition.Build(new Condition.CustomCondition(() => HasAdjacentSuppliedUSNavy), this),
            Condition.Build(new Condition.CustomCondition(() => {
                var b = LastLandBattle;
                return b != null && CountryState.ForId(b.CountryId).Tags.Has(Tag.Buildable, Faction);
            }), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 1));
                discardEvent.IsTrigger = false;
                await discardEvent.Apply();

                int countryId = LastLandBattle.CountryId;
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => {
                var b = LastLandBattle;
                return b != null && CountryState.ForId(b.CountryId).Tags.Has(Tag.Buildable, Faction) && HasAdjacentSuppliedUSNavy;
            }), this))
            .WithGuidance("Discard top 1 deck card to build an Army in the space just battled")
        };
    }
}