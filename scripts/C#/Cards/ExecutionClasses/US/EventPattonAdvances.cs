using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventPattonAdvances : EventCardLogic
{
    private static readonly List<int> battleCountryIds = [(int)Country.Germany, (int)Country.Italy];

    private static readonly List<int> buildCountryIds = [(int)Country.WesternEurope];

    private List<BattleTarget> BattleTargets(BoardState board) => BattleTarget.In(board, battleCountryIds, Faction);

    /// <summary>The build space and the battlegrounds together — the card's two steps.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(buildCountryIds).Plus(TargetSet.FromBattleTargets(BattleTargets(BoardState.Live)));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.CountryFrom(_ => buildCountryIds,
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsBuildable(buildCountryIds, Faction), this))
            .WithGuidance("Build an Army in Western Europe"),
            new ResultStep(this, Choose.BattleTargetFrom(c => BattleTargets(c.Board),
                (target, _) => target.ToAttackChangeEvent(Faction)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(battleCountryIds, Faction), this))
            .WithGuidance("Battle in Germany or Italy"),
        };
    }
}