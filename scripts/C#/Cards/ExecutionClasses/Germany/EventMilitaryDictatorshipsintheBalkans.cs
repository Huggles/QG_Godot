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
            new ResultStep(this, Choose.CountryFrom(() => new List<int> { (int)Country.Balkans },
                countryId => new DeployUnitChangeEvent(Faction.ITALY, countryId, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(new List<int> { (int)Country.Balkans }, Faction.ITALY), this))
            .WithGuidance($"Recruit an Italian army in {CountryState.ForEnum(Country.Balkans).Label}"),

            new ResultStep(this, Choose.UnitFrom(() => AlliedArmiesInUkraine(),
                unitId => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => AlliedArmiesInUkraine().Count > 0), this))
            .WithGuidance($"Eliminate an Allied army in {CountryState.ForEnum(Country.Ukraine).Label}"),
        };
    }
}