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
    private List<BattleTarget> BattleTargets
    {
        get
        {
            var battleLocation = TriggerContextAs<BattleCountryChangeEvent>()?.CountryState;
            if (battleLocation == null) return new List<BattleTarget>();

            var targetCountries = battleLocation.ConnectedCountryStates
                .Append(battleLocation)
                .Distinct()
                .Where(cs => cs.Type == CountryType.LAND && cs.Tags.Has(Tag.Attackable, Faction))
                .ToList();

            var attackableArmyIds = UnitState.AttackableArmyIds(Faction).ToHashSet();
            var targets = targetCountries
                .SelectMany(cs => cs.Units.Values)
                .Where(uId => attackableArmyIds.Contains(uId))
                .Select(uId => new BattleTarget(uId, TargetType.UNIT))
                .ToList();
            targets.AddRange(targetCountries.Select(cs => new BattleTarget(cs.Id, TargetType.COUNTRY)));
            return targets;
        }
    }

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets);

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
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, BattleTargets).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleCountryChange);
            })
            .WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
            .WithGuidance("Battle in the same or adjacent land space"),
        }; 
    }
}