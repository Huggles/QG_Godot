using System.Threading.Tasks;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusBiasForAction : StatusCardLogic
{
    /// <summary>
    /// The space just built in. Scoped to the reaction window's trigger rather than scanned out of the
    /// pool: "the Army just built" is one specific deploy, and a pool scan also picked up recruits and
    /// navy builds made earlier in the same round, widening the target list well past the card text.
    /// Mirrors <see cref="MutatorSyntheticFuelAnyFaction"/>, which implements near-identical text.
    /// </summary>
    private CountryState BuiltCountryState(GameSituation situation) =>
        TriggerContextAs<DeployUnitChangeEvent>(situation)?.CountryState;

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.FactionDeployed(Faction, DeployType.BUILD), this).Immediately(),
            // Built per situation: which spaces are adjacent depends on the deploy being reacted to.
            Condition.Build(new Condition.CustomCondition(s =>
                new Condition.CountryIsAttackable(AdjacentLandCountryIds(s), Faction).MeetCondition(s)), this)
        };
    }

    /// <summary>
    /// Adjacent LAND spaces of the space just built in, listed whole for the trigger's gate.
    ///
    /// Deliberately NOT derived from <see cref="BattleTargets"/>. A country carries Tag.Attackable only
    /// while it is EMPTY — AttackOption.CalculateAttackOptions tags the *units* on an enemy-occupied
    /// space and the *country* only when OccupyingTeam is NONE, and the two branches are mutually
    /// exclusive. So mapping a UNIT target back to its occupied country and testing that country's tag
    /// (what Condition.FactionHasBattleTarget does) can never be true, and the card was unofferable
    /// whenever the only adjacent targets were enemy units — the ordinary front-line case, and the one
    /// a rebuild-in-place always lands in. CountryIsAttackable inspects each country's units itself.
    /// StatusDiveBombers documents the same pitfall.
    /// </summary>
    private List<int> AdjacentLandCountryIds(GameSituation situation)
    {
        CountryState builtIn = BuiltCountryState(situation);
        if (builtIn == null) return new List<int>();

        return situation.Board.AdjacentCountryStates(Faction, builtIn)
            .Where(adj => adj.Type == CountryType.LAND)
            .Select(adj => adj.Id)
            .ToList();
    }

    /// <summary>
    /// "battle a land space adjacent to the Army just built". AdjacentBattleTargets emits a UNIT target
    /// per attackable unit on an occupied space and a COUNTRY target for an empty one, honours
    /// ImmuneForTurn, and requires an adjacent supplied unit — all of which the previous hand-rolled
    /// neighbour scan missed. Its CountryType.LAND filter is what keeps an attackable Navy in an
    /// adjacent sea space out of a list the card text limits to land.
    /// </summary>
    public List<BattleTarget> BattleTargets(GameSituation situation)
    {
        CountryState builtIn = BuiltCountryState(situation);
        if (builtIn == null) return new List<BattleTarget>();

        return situation.Board.AdjacentBattleTargets(Faction, CountryType.LAND, builtIn);
    }

    /// <summary>The land spaces adjacent to the Army just built that this may attack.</summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // The cost, gated on there being something to spend it on. That gate used to be an
            // `if (battleTargets.Count == 0) return;` at the top of the fused step, resolved before
            // the discard so the cost was not paid for a prompt with nothing in it. As a step
            // condition it says the same thing and is visible to Tag.IsExecutable as well.
            //
            // It stays a condition of its OWN, rather than leaning on the card trigger: the trigger
            // gate reads tags while this reads BattleTargets, so the two can disagree at the margin
            // (ImmuneForTurn, supply).
            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 1)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => BattleTargets(s).Count > 0), this))
            .WithGuidance("Battle a land space adjacent to the Army just built"),

            new ResultStep(this, async () => {
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, BattleTargets(GameSituation.Live)).BroadCast();
                if (resp.ResponseCountryIds.Count == 0 && resp.ResponseUnitIds.Count == 0) return CardStepResult.Nothing;

                BattleTarget battleTarget = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = battleTarget.ToAttackChangeEvent(Faction);
                return battleCountryChange;
            })
            .RequiringPreviousStep()
        };
    }
}
