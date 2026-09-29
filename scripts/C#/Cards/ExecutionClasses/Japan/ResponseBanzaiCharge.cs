using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseBanzaiCharge : ResponseCardLogic
{
    /// <summary>
    /// The land spaces this may battle: the one just battled and its neighbours, filtered to what is
    /// attackable. Lifted out of the step closure so <see cref="Targets"/> reads the very list the
    /// step offers — the whole point of the preview.
    /// </summary>
    private List<BattleTarget> BattleTargets(GameSituation situation)
    {
        BoardState board = situation.Board;
        var battleLocation = TriggerContextAs<BattleCountryChangeEvent>(situation)?.CountryState;
        if (battleLocation == null) return new List<BattleTarget>();

        var targetCountries = battleLocation.ConnectedCountryStates
            .Append(battleLocation)
            .Distinct()
            .Where(cs => cs.Type == CountryType.LAND && board.Of(cs).Tags.Has(Tag.Attackable, Faction))
            .ToList();

        var attackableArmyIds = board.AttackableArmyIds(Faction).ToHashSet();
        var targets = targetCountries
            .SelectMany(cs => board.UnitsIn(cs).Values)
            .Where(uId => attackableArmyIds.Contains(uId))
            .Select(uId => new BattleTarget(uId, TargetType.UNIT))
            .ToList();
        targets.AddRange(targetCountries.Select(cs => new BattleTarget(cs.Id, TargetType.COUNTRY)));
        return targets;
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
                (target, _) => target.ToAttackChangeEvent(Faction)))
            .WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
            .WithGuidance("Battle in the same or adjacent land space"),
        }; 
    }
}