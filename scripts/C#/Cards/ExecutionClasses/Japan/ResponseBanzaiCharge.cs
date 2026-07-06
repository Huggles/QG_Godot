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

                List<int> armyUnits = UnitState.AttackableArmyIds(Faction);
                List<int> emptyCountries = targetCountries.Select(c => c.Id).ToList();
                BattleTarget target = await new SelectBattleTargetHandler(emptyCountries, armyUnits).Handle();
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                return battleCountryChange;
            })
            .WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
            .WithGuidance("Battle in the same or adjacent land space"),
        }; 
    }
}