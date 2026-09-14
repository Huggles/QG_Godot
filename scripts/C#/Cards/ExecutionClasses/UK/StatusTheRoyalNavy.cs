using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusTheRoyalNavy : StatusCardLogic
{
    private List<BattleTarget> SeaBattleTargets =>
        CountryState.AttackableSea(Faction)
            .Select(cs => new BattleTarget(cs.Id, TargetType.COUNTRY))
            .Concat(UnitState.AttackableNavies(Faction)
                .Select(us => new BattleTarget(us.Id, TargetType.UNIT)))
            .ToList();

    /// <summary>The sea spaces and enemy navies this may attack a second time.</summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(SeaBattleTargets);

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasBattledAtSea(Faction), this).Immediately(),
            Condition.Build(new Condition.HasSeaBattleTarget(Faction), this),
            Condition.Build(new Condition.CustomCondition(() => DeckState.ForFaction(Faction).HandCardIds.Count >= 2), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, () => Task.FromResult<CardStepResult>(
                new ForceDiscardHandCardsChangeEvent(Faction, Faction, 2)))
            .WithGuidance("Discard 2 cards from hand to battle a sea space"),

            new ResultStep(this, async () => {
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, SeaBattleTargets).BroadCast();
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