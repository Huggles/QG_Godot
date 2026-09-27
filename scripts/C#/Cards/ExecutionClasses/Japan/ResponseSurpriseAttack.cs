using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseSurpriseAttack : ResponseCardLogic
{
    /// <summary>Both battles, sea then land — the same board-wide lists the two steps offer.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(CountryState.AttackableSeaIds(Faction))
            .Plus(TargetSet.Units(UnitState.AttackableNavyIds(Faction)))
            .Plus(TargetSet.Countries(CountryState.AttackableLandIds(Faction)))
            .Plus(TargetSet.Units(UnitState.AttackableArmyIds(Faction)));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { 
            Condition.Build(new Condition.HasBattledAtSea(Faction).Immediately(), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Battle a sea space
            new ResultStep(this, Choose.BattleTargetFrom(() => CountryState.AttackableSeaIds(Faction).Select(id => new BattleTarget(id, TargetType.COUNTRY))
                    .Concat(UnitState.AttackableNavyIds(Faction).Select(id => new BattleTarget(id, TargetType.UNIT))).ToList(),
                target => target.ToAttackChangeEvent(Faction)))
            .WithCondition(()=> Condition.Build(new Condition.HasSeaBattleTarget(Faction), this))
            .WithGuidance("Battle a sea space"),
            
            // Battle a land space
            new ResultStep(this, Choose.BattleTargetFrom(() => CountryState.AttackableLandIds(Faction).Select(id => new BattleTarget(id, TargetType.COUNTRY))
                    .Concat(UnitState.AttackableArmyIds(Faction).Select(id => new BattleTarget(id, TargetType.UNIT))).ToList(),
                target => target.ToAttackChangeEvent(Faction)))
            .WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
            .WithGuidance("Battle a land space"),
        }; 
    }
}