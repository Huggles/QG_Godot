using System.Threading.Tasks;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusDiveBombers : StatusCardLogic
{   
    private CountryState BattledCountryState(GameSituation situation) =>
        TriggerContextAs<BattleCountryChangeEvent>(situation)?.CountryState;

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
    public List<BattleTarget> battleTargets(GameSituation situation)
    {
        CountryState cs = BattledCountryState(situation);
        if (cs == null) return new List<BattleTarget>();

        BoardState board = situation.Board;
        var targets = new List<BattleTarget>();
        // The battled space itself: empty spaces are a COUNTRY target, occupied ones a UNIT target.
        if (board.Of(cs).Tags.Has(Tag.Attackable, Faction))
            targets.Add(new BattleTarget(cs.Id, TargetType.COUNTRY));
        targets.AddRange(board.UnitsIn(cs).Values
            .Where(unitId => board.Of(UnitState.ForId(unitId)).Tags.Has(Tag.Attackable, Faction))
            .Select(unitId => new BattleTarget(unitId, TargetType.UNIT)));

        targets.AddRange(board.AdjacentBattleTargets(Faction, CountryType.LAND, cs));
        return targets.Distinct().ToList();
    }

    /// <summary>
    /// Country ids for the step's executability gate. Deliberately NOT derived from
    /// <see cref="battleTargets"/>: CountryIsAttackable inspects each country's units itself, so the
    /// adjacent spaces must be listed whole. Filtering battleTargets to TargetType.COUNTRY dropped
    /// every occupied space — AdjacentBattleTargets only emits COUNTRY targets for *empty* spaces —
    /// so an adjacent enemy Army yielded an empty list and the card could never become activatable.
    /// </summary>
    public List<int> battleTargetCountryIds(GameSituation situation)
    {
        CountryState cs = BattledCountryState(situation);
        if (cs == null) return new List<int>();

        var ids = new List<int> { cs.Id };
        ids.AddRange(cs.ConnectedCountryStates.Where(adj => adj.Type == CountryType.LAND).Select(adj => adj.Id));
        return ids;
    }

    /// <summary>The space just battled and its land neighbours — what this may hit again.</summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(battleTargets(GameSituation.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 1)))
            .WithGuidance("Battle the same or an adjacent country where it battled this turn")
            .WithCondition(()=>{
                return Condition.Build(new Condition.CustomCondition(s =>
                    new Condition.CountryIsAttackable(battleTargetCountryIds(s), Faction).MeetCondition(s)), this); }),

            new ResultStep(this, Choose.BattleTargetFrom(c => battleTargets(c.Situation),
                (target, c) => target.ToAttackChangeEvent(Faction, c.Board)))
            .RequiringPreviousStep()
        };
    }
}
