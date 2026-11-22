using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusBravado : StatusCardLogic
{
    private List<BattleTarget> LandBattleTargets(BoardState board) =>
        board.AttackableLand(Faction)
            .Select(cs => new BattleTarget(cs.Id, TargetType.COUNTRY))
            .Concat(board.AttackableArmies(Faction)
                .Select(us => new BattleTarget(us.Id, TargetType.UNIT)))
            .Distinct()
            .ToList();

    /// <summary>Every land space and enemy army this may attack.</summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(LandBattleTargets(BoardState.Live));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this),
            Condition.Build(new Condition.HasLandBattleTarget(Faction), this),
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new ResultStep(this, Choose.BattleTargetFrom(c => LandBattleTargets(c.Board),
                (target, c) => target.ToAttackChangeEvent(Faction, c.Board)))
            .WithPrice(StepPrice.SpendPlay(this))
            .WithPrice(StepPrice.DeckDiscard(this, 2))
            .WithGuidance("Discard the top 2 cards of its draw deck to battle a land space")
        };
    }
}