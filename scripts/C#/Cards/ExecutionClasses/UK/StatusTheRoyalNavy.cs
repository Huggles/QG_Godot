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

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasBattledAtSea(Faction), this),
            Condition.Build(new Condition.HasSeaBattleTarget(Faction), this),
            Condition.Build(new Condition.CustomCondition(() => DeckState.ForFaction(Faction).HandCardIds.Count >= 2), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                ForceDiscardHandCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardHandCardsChangeEvent(Faction, Faction, 2));
                discardEvent.IsTrigger = false;
                await discardEvent.ApplyChange();

                BattleTarget battleTarget = await new SelectBattleTargetHandler(SeaBattleTargets).Handle();
                BattleCountryChangeEvent battleEvent = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
                battleEvent.IsTrigger = true;
                return battleEvent;
            })
            .WithGuidance("Discard 2 cards from hand to battle a sea space")
        };
    }
}