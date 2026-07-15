using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventVasilevskyTakesCommandInTheFarEast : EventCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Step 1: Recruit an Army in Vladivostok
            new CardStep(this, async () => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)Country.Vladivostok]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployEvent = BuildChangeEvent(
                    new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployEvent.IsTrigger = true;
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Vladivostok], Faction), this))
            .WithGuidance("Recruit an Army in Vladivostok"),

            // Step 2: Battle in China
            new CardStep(this, async () => {
                var china = CountryState.ForEnum(Country.China);
                List<int> axisUnitsInChina = china.Units.Values
                    .Where(uId => StaticGameData.FactionTeamForFaction(UnitState.ForId(uId).Faction) == FactionTeam.AXIS
                               && UnitState.ForId(uId).IsArmy
                               && !UnitState.ForId(uId).ImmuneForTurn)
                    .ToList();
                List<int> emptyChina = CountryState.AttackableLandIds(Faction).Contains((int)Country.China) && china.Units.Count == 0
                    ? [(int)Country.China] : new List<int>();

                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, emptyChina, axisUnitsInChina).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                return battleCountryChange;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => {
                var china = CountryState.ForEnum(Country.China);
                bool hasAxisUnits = china.Units.Values.Any(uId =>
                    StaticGameData.FactionTeamForFaction(UnitState.ForId(uId).Faction) == FactionTeam.AXIS
                    && UnitState.ForId(uId).IsArmy
                    && !UnitState.ForId(uId).ImmuneForTurn);
                bool canAdvanceToChina = CountryState.AttackableLandIds(Faction).Contains((int)Country.China) && china.Units.Count == 0;
                return hasAxisUnits || canAdvanceToChina;
            }), this))
            .WithGuidance("Battle in China")
        };
    }
}