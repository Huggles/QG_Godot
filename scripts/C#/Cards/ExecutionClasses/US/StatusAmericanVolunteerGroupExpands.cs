using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusAmericanVolunteerGroupExpands : StatusCardLogic, ICountryTagModifier, IUnitSupplyModifier
{
    public bool GrantsSupply(UnitState unit)
    {
        return StaticGameData.FactionTeamForFaction(unit.Faction) == FactionTeam.ALLIES
            && unit.Type == UnitType.ARMY
            && unit.CountryState.Country == Country.Szechuan;
    }

    public void ApplyTagModifiers(Faction faction)
    {
        if (StaticGameData.FactionTeamForFaction(faction) != FactionTeam.ALLIES) return;
        CountryState.ForEnum(Country.Szechuan).AddTag(Tag.Recruitable, faction);
    }

    /// <summary>Szechuan — the space this recruits into, and the space whose Allied Armies it keeps
    /// in supply. Both halves of the card point at the same place.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(new List<Country> { Country.Szechuan })
            .Plus(TargetSet.Units(CountryState.ForEnum(Country.Szechuan).Units.Values
                .Select(UnitState.ForId).Where(GrantsSupply).ToList()));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this),
            Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Szechuan], Faction), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                SpendPlayActionChangeEvent spendEvent = BuildChangeEvent(new SpendPlayActionChangeEvent(Faction));
                spendEvent.IsTrigger = false;
                await spendEvent.Apply();

                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 2));
                discardEvent.IsTrigger = false;
                await discardEvent.Apply();

                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)Country.Szechuan]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Szechuan], Faction), this))
            .WithGuidance("Discard top 2 deck cards to recruit an Army in Szechuan")
        };
    }
}