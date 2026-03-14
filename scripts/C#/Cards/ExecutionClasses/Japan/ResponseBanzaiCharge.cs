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
            Condition.Build(new Condition.HasBattledOnLand(Faction), this) 
        };
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                var battleEvents = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
                    .Where(ce => ce.TriggeringFaction == Faction).ToList();
                var battleLocations = battleEvents.Map(ce => ce.CountryState)
                    .Where(cs => cs.Type == CountryType.LAND).ToList();
                
                // Get same or adjacent land spaces that are attackable
                var targetCountries = battleLocations
                    .SelectMany(bl => bl.NeighborCountryStates.Append(bl))
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