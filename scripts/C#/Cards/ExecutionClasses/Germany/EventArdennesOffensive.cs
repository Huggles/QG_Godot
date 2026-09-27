using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventArdennesOffensive : EventCardLogic
{
    private static readonly List<int> targetCountryIds = [(int)Country.WesternEurope];

    private List<BattleTarget> BattleTargets => BattleTarget.In(targetCountryIds, Faction);

    /// <summary>Both steps operate in Western Europe: what is attackable there, and the space itself
    /// for the build that follows.</summary>
    public override TargetSet Targets() =>
        TargetSet.FromBattleTargets(BattleTargets).Plus(TargetSet.Countries(targetCountryIds));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.BattleTargetFrom(() => BattleTargets,
                target => target.ToAttackChangeEvent(Faction)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(targetCountryIds, Faction), this))
            .WithGuidance("Battle in Western Europe"),
            new ResultStep(this, Choose.CountryFrom(() => targetCountryIds,
                countryId => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsBuildable(targetCountryIds, Faction), this))
            .WithGuidance("Build an Army in Western Europe"),
        };
    }
}
