using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseBanzaiCharge : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { 
            Condition.Build(new Condition.HasBattledOnLand(Faction).Immediately(), this) 
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                var triggerBattle = CardPlayPool.CurrentReactionTrigger as BattleCountryChangeEvent;
                if (triggerBattle == null) return null;
                var battleLocation = triggerBattle.CountryState;
                
                // Get same or adjacent land spaces that are attackable
                var targetCountries = battleLocation.ConnectedCountryStates
                    .Append(battleLocation)
                    .Distinct()
                    .Where(cs => cs.Type == CountryType.LAND && cs.Tags.Has(Tag.Attackable, Faction))
                    .ToList();

                var attackableArmyIds = UnitState.AttackableArmyIds(Faction).ToHashSet();
                List<int> armyUnits = targetCountries
                    .SelectMany(cs => cs.Units.Values)
                    .Where(uId => attackableArmyIds.Contains(uId))
                    .ToList();
                List<int> emptyCountries = targetCountries.Select(c => c.Id).ToList();
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, emptyCountries, armyUnits).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                return battleCountryChange;
            })
            .WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
            .WithGuidance("Battle in the same or adjacent land space"),
        }; 
    }
}