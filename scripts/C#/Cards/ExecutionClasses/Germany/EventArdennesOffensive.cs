using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventArdennesOffensive : EventCardLogic
{
    private static readonly List<int> targetCountryIds = [(int)Country.WesternEurope];

    private List<BattleTarget> BattleTargets(BoardState board) => BattleTarget.In(board, targetCountryIds, Faction);

    /// <summary>Both steps operate in Western Europe: what is attackable there, and the space itself
    /// for the build that follows.</summary>
    public override TargetSet Targets() =>
        TargetSet.FromBattleTargets(BattleTargets(BoardState.Live)).Plus(TargetSet.Countries(targetCountryIds));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.BattleTargetFrom(c => BattleTargets(c.Board),
                (target, _) => target.ToAttackChangeEvent(Faction)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(targetCountryIds, Faction), this))
            .WithGuidance("Battle in Western Europe"),
            new ResultStep(this, Choose.CountryFrom(_ => targetCountryIds,
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsBuildable(targetCountryIds, Faction), this))
            .WithGuidance("Build an Army in Western Europe"),
        };
    }
}
