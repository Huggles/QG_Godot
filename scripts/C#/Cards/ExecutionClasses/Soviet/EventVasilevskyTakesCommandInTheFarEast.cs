using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventVasilevskyTakesCommandInTheFarEast : EventCardLogic
{
    private static readonly List<int> recruitCountryIds = [(int)Country.Vladivostok];

    private static readonly List<int> battleCountryIds = [(int)Country.China];

    /// <summary>Whatever China offers for the battle: the empty country, or the Axis armies in it.</summary>
    private List<BattleTarget> ChinaBattleTargets(BoardState board) => BattleTarget.In(board, battleCountryIds, Faction);

    /// <summary>Vladivostok for the recruit, and whatever China offers for the battle.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(recruitCountryIds).Plus(TargetSet.FromBattleTargets(ChinaBattleTargets(BoardState.Live)));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Step 1: Recruit an Army in Vladivostok
            new ResultStep(this, Choose.CountryFrom(_ => recruitCountryIds,
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(recruitCountryIds, Faction), this))
            .WithGuidance("Recruit an Army in Vladivostok"),

            // Step 2: Battle in China
            new ResultStep(this, Choose.BattleTargetFrom(c => ChinaBattleTargets(c.Board),
                (target, c) => target.ToAttackChangeEvent(Faction, c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(battleCountryIds, Faction), this))
            .WithGuidance("Battle in China")
        };
    }
}
