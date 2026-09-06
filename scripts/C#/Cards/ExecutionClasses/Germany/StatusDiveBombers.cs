using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusDiveBombers : StatusCardLogic
{   
    private CountryState BattledCountryState =>
        TriggerContextAs<BattleCountryChangeEvent>()?.CountryState;

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            // "when you battle a land space" — FactionBattled matches sea battles too, which would
            // then offer the sea space's adjacent LAND targets.
            Condition.Build(new Condition.HasBattledOnLand(Faction), this).Immediately()
        };
    }

    /// <summary>
    /// "the same or adjacent land space to the one battled" — mirrors <see cref="StatusFrontalAssault"/>,
    /// which implements the identical card text.
    /// </summary>
    public List<BattleTarget> battleTargets
    {
        get
        {
            CountryState cs = BattledCountryState;
            if (cs == null) return new List<BattleTarget>();

            var targets = new List<BattleTarget>();
            // The battled space itself: empty spaces are a COUNTRY target, occupied ones a UNIT target.
            if (cs.Tags.Has(Tag.Attackable, Faction))
                targets.Add(new BattleTarget(cs.Id, TargetType.COUNTRY));
            targets.AddRange(cs.Units.Values
                .Where(unitId => UnitState.ForId(unitId).Tags.Has(Tag.Attackable, Faction))
                .Select(unitId => new BattleTarget(unitId, TargetType.UNIT)));

            targets.AddRange(cs.AdjacentBattleTargets(Faction, CountryType.LAND));
            return targets.Distinct().ToList();
        }
    }

    /// <summary>
    /// Country ids for the step's executability gate. Deliberately NOT derived from
    /// <see cref="battleTargets"/>: CountryIsAttackable inspects each country's units itself, so the
    /// adjacent spaces must be listed whole. Filtering battleTargets to TargetType.COUNTRY dropped
    /// every occupied space — AdjacentBattleTargets only emits COUNTRY targets for *empty* spaces —
    /// so an adjacent enemy Army yielded an empty list and the card could never become activatable.
    /// </summary>
    public List<int> battleTargetCountryIds
    {
        get
        {
            CountryState cs = BattledCountryState;
            if (cs == null) return new List<int>();

            var ids = new List<int> { cs.Id };
            ids.AddRange(cs.ConnectedCountryStates.Where(adj => adj.Type == CountryType.LAND).Select(adj => adj.Id));
            return ids;
        }
    }

    /// <summary>The space just battled and its land neighbours — what this may hit again.</summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(battleTargets);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 1));
                discardEvent.IsTrigger = false;
                await discardEvent.Apply();

                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, battleTargets).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChangeEvent = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleCountryChangeEvent);
            })
            .WithGuidance("Battle the same or an adjacent country where you've battle this turn")
            .WithCondition(()=>{
                return Condition.Build(
                    new Condition.CountryIsAttackable(battleTargetCountryIds, Faction), this); })
        };
    }
}
