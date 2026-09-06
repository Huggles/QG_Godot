using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventPattonAdvances : EventCardLogic
{
    private static readonly List<int> battleCountryIds = [(int)Country.Germany, (int)Country.Italy];

    private static readonly List<int> buildCountryIds = [(int)Country.WesternEurope];

    private List<BattleTarget> BattleTargets => BattleTarget.In(battleCountryIds, Faction);

    /// <summary>The build space and the battlegrounds together — the card's two steps.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(buildCountryIds).Plus(TargetSet.FromBattleTargets(BattleTargets));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, buildCountryIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsBuildable(buildCountryIds, Faction), this))
            .WithGuidance("Build an Army in Western Europe"),
            new CardStep(this, async() => {
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, BattleTargets).BroadCast();
                BattleTarget battleTarget = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleEvent = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
                battleEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(battleCountryIds, Faction), this))
            .WithGuidance("Battle in Germany or Italy"),
        };
    }
}