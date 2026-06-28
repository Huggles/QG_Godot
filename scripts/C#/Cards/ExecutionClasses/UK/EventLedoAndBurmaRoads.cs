using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventLedoAndBurmaRoads : EventCardLogic
{
    private static readonly List<int> BattleCountryIds = [(int)Country.China, (int)Country.Szechuan];

    private List<BattleTarget> BattleTargets =>
        BattleCountryIds.SelectMany(id => {
            var cs = CountryState.ForId(id);
            var list = new List<BattleTarget>();
            if (cs.Tags.Has(Tag.Attackable, Faction))
                list.Add(new BattleTarget(id, TargetType.COUNTRY));
            list.AddRange(cs.Units.Values
                .Where(uId => UnitState.ForId(uId).Tags.Has(Tag.Attackable, Faction))
                .Select(uId => new BattleTarget(uId, TargetType.UNIT)));
            return list;
        }).ToList();

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Build an Army in Southeast Asia
            new CardStep(this, async () => {
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(
                    new DeployUnitChangeEvent(Faction, (int)Country.SouthEastAsia, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsBuildable([(int)Country.SouthEastAsia], Faction), this))
            .WithGuidance("Build an Army in Southeast Asia"),

            // Battle in China or Szechuan
            new CardStep(this, async () => {
                BattleTarget battleTarget = await new SelectBattleTargetHandler(BattleTargets).Handle();
                BattleCountryChangeEvent battleEvent = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
                battleEvent.IsTrigger = true;
                return battleEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(BattleCountryIds, Faction), this))
            .WithGuidance("Battle in China or Szechuan")
        };
    }
}