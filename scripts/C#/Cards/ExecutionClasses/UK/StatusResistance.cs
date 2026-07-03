using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusResistance : StatusCardLogic
{
    private static readonly List<int> TargetCountryIds = [(int)Country.WesternEurope, (int)Country.Italy];

    private List<BattleTarget> BattleTargets =>
        TargetCountryIds.SelectMany(id => {
            var cs = CountryState.ForId(id);
            var list = new List<BattleTarget>();
            if (cs.Tags.Has(Tag.Attackable, Faction))
                list.Add(new BattleTarget(id, TargetType.COUNTRY));
            list.AddRange(cs.Units.Values
                .Where(uId => UnitState.ForId(uId).Tags.Has(Tag.Attackable, Faction))
                .Select(uId => new BattleTarget(uId, TargetType.UNIT)));
            return list;
        }).ToList();

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.CountryIsAttackable(TargetCountryIds, Faction), this),
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

                BattleTarget battleTarget = await new SelectBattleTargetHandler(BattleTargets).Handle();
                BattleCountryChangeEvent battleEvent = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
                battleEvent.IsTrigger = true;
                return battleEvent;
            })
            .WithGuidance("Discard 2 cards from hand to battle in Western Europe or Italy")
        };
    }
}