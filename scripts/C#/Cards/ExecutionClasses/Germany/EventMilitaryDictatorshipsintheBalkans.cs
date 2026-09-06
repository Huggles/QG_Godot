using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventMilitaryDictatorshipsInTheBalkans : EventCardLogic
{
    private List<int> AlliedArmiesInUkraine() =>
        CountryState.ForEnum(Country.Ukraine).Units.Values
        .Where(unitId => StaticGameData.FactionTeamForFaction(UnitState.ForId(unitId).Faction) == FactionTeam.ALLIES).ToList();

    /// <summary>The Balkans recruit and the Ukraine elimination — the card's two steps.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(new List<Country> { Country.Balkans })
            .Plus(TargetSet.Units(AlliedArmiesInUkraine()));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async () =>
            {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, new List<int> { (int)Country.Balkans }).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction.ITALY, selectedCountryId, DeployType.RECRUIT));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(new List<int> { (int)Country.Balkans }, Faction.ITALY), this))
            .WithGuidance($"Recruit an Italian army in {CountryState.ForEnum(Country.Balkans).Label}"),

            new CardStep(this, async () =>
            {
                List<int> alliedArmies = AlliedArmiesInUkraine();
                int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, alliedArmies).BroadCast()).ResponseUnitIds[0];
                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(removeEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => AlliedArmiesInUkraine().Count > 0), this))
            .WithGuidance($"Eliminate an Allied army in {CountryState.ForEnum(Country.Ukraine).Label}"),
        };
    }
}