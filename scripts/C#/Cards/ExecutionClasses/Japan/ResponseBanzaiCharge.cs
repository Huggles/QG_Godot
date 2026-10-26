using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseBanzaiCharge : ResponseCardLogic
{
    /// <summary>
    /// What is attackable in the land space just battled and its neighbours. Lifted out of the step
    /// closure so <see cref="Targets"/> reads the very list the step offers. The country tag only marks
    /// EMPTY spaces, so filtering countries on it first dropped every enemy army; BattleTarget.In reads both.
    /// </summary>
    private List<BattleTarget> BattleTargets(GameSituation situation)
    {
        BoardState board = situation.Board;
        var battleLocation = TriggerContextAs<BattleCountryChangeEvent>(situation)?.CountryState;
        if (battleLocation == null) return new List<BattleTarget>();

        var landIds = board.AdjacentCountryStates(Faction, battleLocation)
            .Append(battleLocation)
            .Where(cs => cs.Type == CountryType.LAND)
            .Select(cs => cs.Id)
            .Distinct();
        return BattleTarget.In(board, landIds, Faction);
    }

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { 
            Condition.Build(new Condition.HasBattledOnLand(Faction).Immediately(), this) 
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new ResultStep(this, Choose.BattleTargetFrom(c => BattleTargets(c.Situation),
                (target, c) => target.ToAttackChangeEvent(Faction, c.Board)))
            .WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
            .WithGuidance("Battle in the same or adjacent land space"),
        }; 
    }
}