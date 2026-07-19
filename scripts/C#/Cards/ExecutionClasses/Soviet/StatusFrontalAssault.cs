using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusFrontalAssault : StatusCardLogic
{
    private BattleCountryChangeEvent LastLandBattle =>
        CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
            .LastOrDefault(ce => ce.TriggeringFaction == Faction && ce.CountryState.Type == CountryType.LAND);

    private List<BattleTarget> SameOrAdjacentTargets
    {
        get
        {
            var battle = LastLandBattle;
            if (battle == null) return new List<BattleTarget>();
            var cs = battle.CountryState;
            var targets = new List<BattleTarget>();
            // Same space
            if (cs.Tags.Has(Tag.Attackable, Faction))
                targets.Add(new BattleTarget(cs.Id, TargetType.COUNTRY));
            targets.AddRange(cs.Units.Values
                .Where(uId => UnitState.ForId(uId).Tags.Has(Tag.Attackable, Faction))
                .Select(uId => new BattleTarget(uId, TargetType.UNIT)));
            // Adjacent land spaces
            targets.AddRange(cs.AdjacentBattleTargets(Faction, CountryType.LAND));
            return targets.Distinct().ToList();
        }
    }

    private List<int> SameOrAdjacentCountryIds
    {
        get
        {
            var battle = LastLandBattle;
            if (battle == null) return new List<int>();
            var cs = battle.CountryState;
            var ids = new List<int> { cs.Id };
            ids.AddRange(cs.ConnectedCountryStates.Where(adj => adj.Type == CountryType.LAND).Select(adj => adj.Id));
            return ids;
        }
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasBattledOnLand(Faction), this).Immediately(),
            Condition.Build(new Condition.CustomCondition(() =>
                DeckState.ForFaction(Faction).HandCardIds.Count >= 2), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                ForceDiscardHandCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardHandCardsChangeEvent(Faction, Faction, 2));
                discardEvent.IsTrigger = false;
                await discardEvent.ApplyChange();

                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, SameOrAdjacentTargets).BroadCast();
                BattleTarget battleTarget = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleEvent = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
                battleEvent.IsTrigger = true;
                return battleEvent;
            })
            .WithGuidance("Discard 2 cards from hand to battle the same or adjacent land space")
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(SameOrAdjacentCountryIds, Faction), this))
        };
    }
}