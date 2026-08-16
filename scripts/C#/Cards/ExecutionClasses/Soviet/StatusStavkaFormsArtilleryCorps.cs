using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusStavkaFormsArtilleryCorps : StatusCardLogic
{
    private BattleCountryChangeEvent LastLandBattle =>
        CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
            .LastOrDefault(ce => ce.IsBattle && ce.TriggeringFaction == Faction && ce.CountryState.Type == CountryType.LAND);

    private List<BattleTarget> SameSpaceTargets
    {
        get
        {
            var battle = LastLandBattle;
            if (battle == null) return new List<BattleTarget>();
            var cs = battle.CountryState;
            var targets = new List<BattleTarget>();
            if (cs.Tags.Has(Tag.Attackable, Faction))
                targets.Add(new BattleTarget(cs.Id, TargetType.COUNTRY));
            targets.AddRange(cs.Units.Values
                .Where(uId => UnitState.ForId(uId).Tags.Has(Tag.Attackable, Faction))
                .Select(uId => new BattleTarget(uId, TargetType.UNIT)));
            return targets;
        }
    }

    private List<int> SameSpaceCountryIds
    {
        get
        {
            var battle = LastLandBattle;
            return battle != null ? new List<int> { battle.CountryId } : new List<int>();
        }
    }

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasBattledOnLand(Faction), this).Immediately(),
            Condition.Build(new Condition.CustomCondition(() =>
                DeckState.ForFaction(Faction).HandCardIds.Count >= 1), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                ForceDiscardHandCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardHandCardsChangeEvent(Faction, Faction, 1));
                discardEvent.IsTrigger = false;
                await discardEvent.Apply();

                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, SameSpaceTargets).BroadCast();
                BattleTarget battleTarget = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleEvent = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
                battleEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleEvent);
            })
            .WithGuidance("Discard a card from hand to battle the same space")
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(SameSpaceCountryIds, Faction), this))
        };
    }
}