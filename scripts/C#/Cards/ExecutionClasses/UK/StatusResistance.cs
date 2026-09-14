using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusResistance : StatusCardLogic
{
    private static readonly List<int> TargetCountryIds = [(int)Country.WesternEurope, (int)Country.Italy];

    private List<BattleTarget> BattleTargets => BattleTarget.In(TargetCountryIds, Faction);

    /// <summary>What is attackable in Western Europe and Italy.</summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets);

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this),
            Condition.Build(new Condition.CountryIsAttackable(TargetCountryIds, Faction), this),
            Condition.Build(new Condition.CustomCondition(() => DeckState.ForFaction(Faction).HandCardIds.Count >= 2), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, () => Task.FromResult<CardStepResult>(
                new SpendPlayActionChangeEvent(Faction)))
            .WithGuidance("Discard 2 cards from hand to battle in Western Europe or Italy"),

            new RequirementStep(this, () => Task.FromResult<CardStepResult>(
                new ForceDiscardHandCardsChangeEvent(Faction, Faction, 2)))
            .RequiringPreviousStep(),

            new ResultStep(this, async () => {
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, BattleTargets).BroadCast();
                BattleTarget battleTarget = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleEvent = battleTarget.ToAttackChangeEvent(Faction);
                return battleEvent;
            })
            .RequiringPreviousStep()
        };
    }
}