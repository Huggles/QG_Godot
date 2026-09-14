using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusBravado : StatusCardLogic
{
    private List<BattleTarget> LandBattleTargets =>
        CountryState.AttackableLand(Faction)
            .Select(cs => new BattleTarget(cs.Id, TargetType.COUNTRY))
            .Concat(UnitState.AttackableArmies(Faction)
                .Select(us => new BattleTarget(us.Id, TargetType.UNIT)))
            .Distinct()
            .ToList();

    /// <summary>Every land space and enemy army this may attack.</summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(LandBattleTargets);

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
            new RequirementStep(this, () => Task.FromResult<CardStepResult>(
                new SpendPlayActionChangeEvent(Faction)))
            .WithGuidance("Discard the top 2 cards of your draw deck to battle a land space"),

            new RequirementStep(this, () => Task.FromResult<CardStepResult>(
                new ForceDiscardCardsChangeEvent(Faction, Faction, 2)))
            .RequiringPreviousStep(),

            new ResultStep(this, async () => {
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, LandBattleTargets).BroadCast();
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