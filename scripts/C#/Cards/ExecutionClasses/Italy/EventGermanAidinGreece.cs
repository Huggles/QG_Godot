using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventGermanAidinGreece : EWCardLogic
{
    public List<Country> targetCountries = [Country.Balkans];
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                var alliedArmyTargets = GameSession.Current.GameState.UnitStatesById.Values
                    .Where(us => StaticGameData.FactionTeamForFaction(us.Faction) == FactionTeam.ALLIES
                               && us.Type == UnitType.ARMY
                               && us.CountryState.Country == targetCountries[0])
                    .Select(us => new BattleTarget(us.Id, TargetType.UNIT))
                    .ToList();
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, alliedArmyTargets).BroadCast();
                BattleCountryChangeEvent battleEvent = BuildChangeEvent(new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT).ToAttackChangeEvent(Faction));
                battleEvent.IsTrigger = true;
                return battleEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryHasEnemyUnit((int)targetCountries[0], Faction),this))
            .WithGuidance($"Eliminate an Allied army in {CountryState.ForEnum(targetCountries[0]).Label}"),
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)targetCountries[0]]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction.GERMANY, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)targetCountries[0]], Faction.GERMANY),this))
            .WithGuidance($"Recruit a German army in {CountryState.ForEnum(targetCountries[0]).Label}"),
        };
        
    }
}