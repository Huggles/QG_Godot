using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusResistance : StatusCardLogic
{
    private static readonly List<int> TargetCountryIds = [(int)Country.WesternEurope, (int)Country.Italy];

    private List<BattleTarget> BattleTargets(BoardState board) => BattleTarget.In(board, TargetCountryIds, Faction);

    /// <summary>What is attackable in Western Europe and Italy.</summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(BoardState.Live));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this),
            Condition.Build(new Condition.CountryIsAttackable(TargetCountryIds, Faction), this),
            Condition.Build(new Condition.CustomCondition(s => s.Board.ForFaction(Faction).Hand.Count >= 2), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(_ => new SpendPlayActionChangeEvent(Faction)))
            .WithGuidance("Discard 2 cards from hand to battle in Western Europe or Italy"),

            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardHandCardsChangeEvent(Faction, Faction, 2)))
            .RequiringPreviousStep(),

            new ResultStep(this, Choose.BattleTargetFrom(c => BattleTargets(c.Board),
                (target, c) => target.ToAttackChangeEvent(Faction, c.Board)))
            .RequiringPreviousStep()
        };
    }
}