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

    /// <summary>The space just battled — the only place this may strike again.</summary>
    public override TargetSet Targets() => TargetSet.FromBattleTargets(SameSpaceTargets);

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
            new RequirementStep(this, Choose.Fixed(() => new ForceDiscardHandCardsChangeEvent(Faction, Faction, 1)))
            .WithGuidance("Discard a card from hand to battle the same space")
            .WithCondition(() => Condition.Build(new Condition.CountryIsAttackable(SameSpaceCountryIds, Faction), this)),

            new ResultStep(this, Choose.BattleTargetFrom(() => SameSpaceTargets,
                target => target.ToAttackChangeEvent(Faction)))
            .RequiringPreviousStep()
        };
    }
}