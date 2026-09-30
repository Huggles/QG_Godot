using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusAircraftCarriers : StatusCardLogic
{
    private BattleCountryChangeEvent LastSeaBattle(GameSituation situation) =>
        situation.PoolEvents<BattleCountryChangeEvent>()
            .LastOrDefault(ce => ce.IsBattle && ce.TriggeringFaction == Faction && ce.CountryState.Type == CountryType.SEA);

    /// <summary>The sea space just battled, where the new Navy appears. The trigger picks it:
    /// this card offers no selection.</summary>
    public override TargetSet Targets() =>
        LastSeaBattle(GameSituation.Live) == null
            ? TargetSet.None
            : TargetSet.Countries(new List<int> { LastSeaBattle(GameSituation.Live).CountryId });

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasBattledAtSea(Faction), this).Immediately(),
            Condition.Build(new Condition.CardHasNotBeenActivatedThisTurn(CardState), this),
            Condition.Build(new Condition.CustomCondition(s => {
                var battle = LastSeaBattle(s);
                return battle != null && s.Board.Of(CountryState.ForId(battle.CountryId)).Tags.Has(Tag.Buildable, Faction);
            }), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 1)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => {
                var battle = LastSeaBattle(s);
                return battle != null && s.Board.Of(CountryState.ForId(battle.CountryId)).Tags.Has(Tag.Buildable, Faction);
            }), this))
            .WithGuidance("Discard top 1 deck card to build a Navy in the sea space just battled"),

            new ResultStep(this, Choose.Fixed(c => new DeployUnitChangeEvent(Faction, LastSeaBattle(c.Situation).CountryId, DeployType.BUILD)))
            .RequiringPreviousStep()
        };
    }
}
