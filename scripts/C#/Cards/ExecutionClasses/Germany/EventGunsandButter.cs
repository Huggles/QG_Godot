using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventGunsAndButter : EventCardLogic
{
    private const string OPT_BUILD_ARMY  = "Build Army";
    private const string OPT_BUILD_NAVY  = "Build Navy";
    private const string OPT_LAND_BATTLE = "Land Battle";
    private const string OPT_SEA_BATTLE  = "Sea Battle";

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                // Build available options
                List<string> options = new();
                if (CountryState.BuildableLand(Faction).Any())                                           options.Add(OPT_BUILD_ARMY);
                if (CountryState.BuildableSea(Faction).Any())                                            options.Add(OPT_BUILD_NAVY);
                if (UnitState.AttackableArmies(Faction).Any() || CountryState.AttackableLand(Faction).Any()) options.Add(OPT_LAND_BATTLE);
                if (UnitState.AttackableNavies(Faction).Any() || CountryState.AttackableSea(Faction).Any())  options.Add(OPT_SEA_BATTLE);

                var actionResp = await new InputRequest.SelectOptionRequestHandler(
                    Faction, options, "Choose an action").BroadCast();
                string selected = options[actionResp.ResponseCardIds[0]];

                // Execute the chosen action
                ChangeEvent result = null;
                if (selected == OPT_BUILD_ARMY)
                {
                    int armyCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, CountryState.BuildableLand(Faction).ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                    result = BuildChangeEvent(new DeployUnitChangeEvent(Faction, armyCountryId, DeployType.BUILD));
                }
                else if (selected == OPT_BUILD_NAVY)
                {
                    int navyCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, CountryState.BuildableSea(Faction).ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                    result = BuildChangeEvent(new DeployUnitChangeEvent(Faction, navyCountryId, DeployType.BUILD));
                }
                else if (selected == OPT_LAND_BATTLE)
                {
                    var landResp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, CountryState.AttackableLandIds(Faction), UnitState.AttackableArmyIds(Faction)).BroadCast();
                    BattleTarget landTarget = landResp.ResponseCountryIds.Count > 0
                        ? new BattleTarget(landResp.ResponseCountryIds[0], TargetType.COUNTRY)
                        : new BattleTarget(landResp.ResponseUnitIds[0], TargetType.UNIT);
                    result = BuildChangeEvent(landTarget.ToAttackChangeEvent(Faction));
                }
                else if (selected == OPT_SEA_BATTLE)
                {
                    var seaResp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, CountryState.AttackableSeaIds(Faction), UnitState.AttackableNavyIds(Faction)).BroadCast();
                    BattleTarget seaTarget = seaResp.ResponseCountryIds.Count > 0
                        ? new BattleTarget(seaResp.ResponseCountryIds[0], TargetType.COUNTRY)
                        : new BattleTarget(seaResp.ResponseUnitIds[0], TargetType.UNIT);
                    result = BuildChangeEvent(seaTarget.ToAttackChangeEvent(Faction));
                }

                if (result != null)
                    result.IsTrigger = true;

                return result;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
                CountryState.BuildableLand(Faction).Any()
                || CountryState.BuildableSea(Faction).Any()
                || UnitState.AttackableArmies(Faction).Any() || CountryState.AttackableLand(Faction).Any()
                || UnitState.AttackableNavies(Faction).Any() || CountryState.AttackableSea(Faction).Any()
            ), this))
            .WithGuidance("Use this card to: build an army, build a navy, land battle, or sea battle")
        };
    }
}