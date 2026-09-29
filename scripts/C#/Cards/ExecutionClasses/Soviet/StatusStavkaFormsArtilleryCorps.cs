using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusStavkaFormsArtilleryCorps : StatusCardLogic
{
    private BattleCountryChangeEvent LastLandBattle(GameSituation situation) =>
        situation.PoolEvents<BattleCountryChangeEvent>()
            .LastOrDefault(ce => ce.IsBattle && ce.TriggeringFaction == Faction && ce.CountryState.Type == CountryType.LAND);

    private List<BattleTarget> SameSpaceTargets(GameSituation situation)
    {
        var battle = LastLandBattle(situation);
        if (battle == null) return new List<BattleTarget>();
        var board = situation.Board;
        var cs = battle.CountryState;
        var targets = new List<BattleTarget>();
        if (board.Of(cs).Tags.Has(Tag.Attackable, Faction))
            targets.Add(new BattleTarget(cs.Id, TargetType.COUNTRY));
        targets.AddRange(board.UnitsIn(cs).Values
            .Where(uId => board.Of(UnitState.ForId(uId)).Tags.Has(Tag.Attackable, Faction))
            .Select(uId => new BattleTarget(uId, TargetType.UNIT)));
        return targets;
    }

    private List<int> SameSpaceCountryIds(GameSituation situation)
    {
        var battle = LastLandBattle(situation);
        return battle != null ? new List<int> { battle.CountryId } : new List<int>();
    }

    /// <summary>The space just battled — the only place this may strike again.</summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(SameSpaceTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasBattledOnLand(Faction), this).Immediately(),
            Condition.Build(new Condition.CustomCondition(s =>
                s.Board.ForFaction(Faction).Hand.Count >= 1), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardHandCardsChangeEvent(Faction, Faction, 1)))
            .WithGuidance("Discard a card from hand to battle the same space")
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
                new Condition.CountryIsAttackable(SameSpaceCountryIds(s), Faction).MeetCondition(s)), this)),

            new ResultStep(this, Choose.BattleTargetFrom(c => SameSpaceTargets(c.Situation),
                (target, _) => target.ToAttackChangeEvent(Faction)))
            .RequiringPreviousStep()
        };
    }
}