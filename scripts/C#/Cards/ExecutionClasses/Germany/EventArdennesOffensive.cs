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
