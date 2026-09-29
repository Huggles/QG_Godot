using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusFrontalAssault : StatusCardLogic
{
    private BattleCountryChangeEvent LastLandBattle(GameSituation situation) =>
        situation.PoolEvents<BattleCountryChangeEvent>()
            .LastOrDefault(ce => ce.IsBattle && ce.TriggeringFaction == Faction && ce.CountryState.Type == CountryType.LAND);

    private List<BattleTarget> SameOrAdjacentTargets(GameSituation situation)
    {
        var battle = LastLandBattle(situation);
        if (battle == null) return new List<BattleTarget>();
        var board = situation.Board;
        var cs = battle.CountryState;
        var targets = new List<BattleTarget>();
        // Same space
        if (board.Of(cs).Tags.Has(Tag.Attackable, Faction))
            targets.Add(new BattleTarget(cs.Id, TargetType.COUNTRY));
        targets.AddRange(board.UnitsIn(cs).Values
            .Where(uId => board.Of(UnitState.ForId(uId)).Tags.Has(Tag.Attackable, Faction))
            .Select(uId => new BattleTarget(uId, TargetType.UNIT)));
        // Adjacent land spaces
        targets.AddRange(board.AdjacentBattleTargets(Faction, CountryType.LAND, cs));
        return targets.Distinct().ToList();
    }

    private List<int> SameOrAdjacentCountryIds(GameSituation situation)
    {
        var battle = LastLandBattle(situation);
        if (battle == null) return new List<int>();
        var cs = battle.CountryState;
        var ids = new List<int> { cs.Id };
        ids.AddRange(cs.ConnectedCountryStates.Where(adj => adj.Type == CountryType.LAND).Select(adj => adj.Id));
        return ids;
    }

    /// <summary>The space just battled and its land neighbours — what this may hit again.</summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(SameOrAdjacentTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasBattledOnLand(Faction), this).Immediately(),
            Condition.Build(new Condition.CustomCondition(s =>
                s.Board.ForFaction(Faction).Hand.Count >= 2), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardHandCardsChangeEvent(Faction, Faction, 2)))
            .WithGuidance("Discard 2 cards from hand to battle the same or adjacent land space")
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
                new Condition.CountryIsAttackable(SameOrAdjacentCountryIds(s), Faction).MeetCondition(s)), this)),

            new ResultStep(this, Choose.BattleTargetFrom(c => SameOrAdjacentTargets(c.Situation),
                (target, _) => target.ToAttackChangeEvent(Faction)))
            .RequiringPreviousStep()
        };
    }
}