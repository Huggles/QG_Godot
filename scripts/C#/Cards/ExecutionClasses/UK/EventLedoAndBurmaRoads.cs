using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventLedoAndBurmaRoads : EventCardLogic
{
    private static readonly List<int> BattleCountryIds = [(int)Country.China, (int)Country.Szechuan];

    private static readonly List<int> BuildCountryIds = [(int)Country.SouthEastAsia];

    private List<BattleTarget> BattleTargets => BattleTarget.In(BattleCountryIds, Faction);

    /// <summary>The build space, and what is attackable in China and Szechuan.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(BuildCountryIds).Plus(TargetSet.FromBattleTargets(BattleTargets));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Build an Army in Southeast Asia
            new ResultStep(this, Choose.Fixed(() => new DeployUnitChangeEvent(Faction, BuildCountryIds[0], DeployType.BUILD)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsBuildable(BuildCountryIds, Faction), this))
            .WithGuidance("Build an Army in Southeast Asia"),

            // Battle in China or Szechuan
            new ResultStep(this, Choose.BattleTargetFrom(() => BattleTargets,
                target => target.ToAttackChangeEvent(Faction)))
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(BattleCountryIds, Faction), this))
            .WithGuidance("Battle in China or Szechuan")
        };
    }
}