using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusTheRoyalNavy : StatusCardLogic
{
    private List<BattleTarget> SeaBattleTargets(BoardState board) =>
        board.AttackableSea(Faction)
            .Select(cs => new BattleTarget(cs.Id, TargetType.COUNTRY))
            .Concat(board.AttackableNavies(Faction)
                .Select(us => new BattleTarget(us.Id, TargetType.UNIT)))
            .ToList();

    /// <summary>The sea spaces and enemy navies this may attack a second time.</summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(SeaBattleTargets(BoardState.Live));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasBattledAtSea(Faction), this).Immediately(),
            Condition.Build(new Condition.HasSeaBattleTarget(Faction), this),
            Condition.Build(new Condition.CustomCondition(s => s.Board.ForFaction(Faction).Hand.Count >= 2), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardHandCardsChangeEvent(Faction, Faction, 2)))
            .WithGuidance("Discard 2 cards from hand to battle a sea space"),

            new ResultStep(this, Choose.BattleTargetFrom(c => SeaBattleTargets(c.Board),
                (target, c) => target.ToAttackChangeEvent(Faction, c.Board)))
            .RequiringPreviousStep()
        };
    }
}