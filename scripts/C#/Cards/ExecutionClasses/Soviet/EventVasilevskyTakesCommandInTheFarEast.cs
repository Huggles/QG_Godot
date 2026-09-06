using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventVasilevskyTakesCommandInTheFarEast : EventCardLogic
{
    private static readonly List<int> recruitCountryIds = [(int)Country.Vladivostok];

    /// <summary>
    /// Step 2's offer: the Axis armies standing in China, or China itself when it is empty and
    /// reachable. Kept as one expression so the step, its condition and <see cref="Targets"/> agree —
    /// note this is narrower than BattleTarget.In, which does not require the country to be empty.
    /// </summary>
    private List<BattleTarget> ChinaBattleTargets
    {
        get
        {
            var china = CountryState.ForEnum(Country.China);
            var targets = china.Units.Values
                .Where(uId => StaticGameData.FactionTeamForFaction(UnitState.ForId(uId).Faction) == FactionTeam.AXIS
                           && UnitState.ForId(uId).IsArmy
                           && !UnitState.ForId(uId).ImmuneForTurn)
                .Select(uId => new BattleTarget(uId, TargetType.UNIT))
                .ToList();
            if (CountryState.AttackableLandIds(Faction).Contains((int)Country.China) && china.Units.Count == 0)
                targets.Add(new BattleTarget((int)Country.China, TargetType.COUNTRY));
            return targets;
        }
    }

    /// <summary>Vladivostok for the recruit, and whatever China offers for the battle.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(recruitCountryIds).Plus(TargetSet.FromBattleTargets(ChinaBattleTargets));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Step 1: Recruit an Army in Vladivostok
            new CardStep(this, async () => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, recruitCountryIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(
                    new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(recruitCountryIds, Faction), this))
            .WithGuidance("Recruit an Army in Vladivostok"),

            // Step 2: Battle in China
            new CardStep(this, async () => {
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, ChinaBattleTargets).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleCountryChange);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => ChinaBattleTargets.Count > 0), this))
            .WithGuidance("Battle in China")
        };
    }
}