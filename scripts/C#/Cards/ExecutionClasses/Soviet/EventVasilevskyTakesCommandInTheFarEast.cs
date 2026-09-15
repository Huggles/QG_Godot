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
    private List<BattleTarget> ChinaBattleTargets => BattleTarget.In(battleCountryIds, Faction);

    /// <summary>Vladivostok for the recruit, and whatever China offers for the battle.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(recruitCountryIds).Plus(TargetSet.FromBattleTargets(ChinaBattleTargets));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Step 1: Recruit an Army in Vladivostok
            new ResultStep(this, async () => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, recruitCountryIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = 
                    new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT);
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(recruitCountryIds, Faction), this))
            .WithGuidance("Recruit an Army in Vladivostok"),

            // Step 2: Battle in China
            new ResultStep(this, async () => {
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, ChinaBattleTargets).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = target.ToAttackChangeEvent(Faction);
                return battleCountryChange;
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(battleCountryIds, Faction), this))
            .WithGuidance("Battle in China")
        };
    }
}
