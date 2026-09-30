using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusAmphibiousLandings : StatusCardLogic
{
    private BattleCountryChangeEvent LastLandBattle(GameSituation situation) =>
        situation.PoolEvents<BattleCountryChangeEvent>()
            .LastOrDefault(ce => ce.IsBattle && ce.TriggeringFaction == Faction && ce.CountryState.Type == CountryType.LAND);

    private bool HasAdjacentSuppliedUSNavy(GameSituation situation) =>
        LastLandBattle(situation) != null && CountryState.ForId(LastLandBattle(situation).CountryId).ConnectedCountryStates
            .Any(adj => situation.Board.ActiveUnits(Faction)
                .Any(us => us.Type == UnitType.NAVY && situation.Board.CountryOf(us) == adj.Id && situation.Board.InSupply(us)));

    /// <summary>The land space just battled, where the new Army appears. The trigger picks it:
    /// this card offers no selection.</summary>
    public override TargetSet Targets() =>
        LastLandBattle(GameSituation.Live) == null
            ? TargetSet.None
            : TargetSet.Countries(new List<int> { LastLandBattle(GameSituation.Live).CountryId });

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasBattledOnLand(Faction), this).Immediately(),
            Condition.Build(new Condition.CardHasNotBeenActivatedThisTurn(CardState), this),
            Condition.Build(new Condition.CustomCondition(s => HasAdjacentSuppliedUSNavy(s)), this),
            Condition.Build(new Condition.CustomCondition(s => {
                var b = LastLandBattle(s);
                return b != null && s.Board.Of(CountryState.ForId(b.CountryId)).Tags.Has(Tag.Buildable, Faction);
            }), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 1)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => {
                var b = LastLandBattle(s);
                return b != null && s.Board.Of(CountryState.ForId(b.CountryId)).Tags.Has(Tag.Buildable, Faction) && HasAdjacentSuppliedUSNavy(s);
            }), this))
            .WithGuidance("Discard top 1 deck card to build an Army in the space just battled"),

            new ResultStep(this, Choose.Fixed(c => new DeployUnitChangeEvent(Faction, LastLandBattle(c.Situation).CountryId, DeployType.BUILD)))
            .RequiringPreviousStep()
        };
    }
}
