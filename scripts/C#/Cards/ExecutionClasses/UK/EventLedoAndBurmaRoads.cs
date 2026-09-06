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
            new CardStep(this, async () => {
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(
                    new DeployUnitChangeEvent(Faction, BuildCountryIds[0], DeployType.BUILD));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsBuildable(BuildCountryIds, Faction), this))
            .WithGuidance("Build an Army in Southeast Asia"),

            // Battle in China or Szechuan
            new CardStep(this, async () => {
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, BattleTargets).BroadCast();
                BattleTarget battleTarget = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleEvent = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
                battleEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(BattleCountryIds, Faction), this))
            .WithGuidance("Battle in China or Szechuan")
        };
    }
}