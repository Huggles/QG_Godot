using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventArdennesOffensive : EventCardLogic
{
    private static readonly List<int> targetCountryIds = [(int)Country.WesternEurope];

    private List<BattleTarget> BattleTargets =>
        targetCountryIds.SelectMany(id => {
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
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, BattleTargets).BroadCast();
                BattleTarget battleTarget = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleEvent = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
                battleEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(targetCountryIds, Faction), this))
            .WithGuidance("Battle in Western Europe"),
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, targetCountryIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent)   ;
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsBuildable(targetCountryIds, Faction), this))
            .WithGuidance("Build an Army in Western Europe"),
        };
    }
}
