using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseDestroyerTransport : ResponseCardLogic
{
    // Captured in step 1 so that step 2 can reference the same location even
    // after CurrentReactionTrigger is restored between steps.
    private CountryState _triggerSeaLocation;

    /// <summary>
    /// The sea space that was battled. Step 1 caches it into the field above because
    /// CurrentReactionTrigger is restored before step 2 runs; at hover time nothing is cached yet, so
    /// this falls back to the live trigger — which is precisely what the preview needs.
    /// </summary>
    private CountryState TriggerSeaLocation(GameSituation situation) =>
        _triggerSeaLocation ?? TriggerContextAs<BattleCountryChangeEvent>(situation)?.CountryState;

    /// <summary>The land spaces beside that sea space that can actually take an Army. Both steps
    /// offer this same list, and so does <see cref="Targets"/>.</summary>
    private List<CountryState> AdjacentBuildable(GameSituation situation) =>
        TriggerSeaLocation(situation) == null
            ? new List<CountryState>()
            : situation.Board.BuildableLand(Faction)
                .Where(cs => TriggerSeaLocation(situation).ConnectedCountryStates.Contains(cs))
                .ToList();

    public override TargetSet Targets() => TargetSet.Countries(AdjacentBuildable(GameSituation.Live));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { 
            Condition.Build(new Condition.HasBattledAtSea(Faction).Immediately(), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Build first Army adjacent to the battled sea space that triggered this reaction
            new ResultStep(this, async() => {
                var triggerBattle = TriggerContextAs<BattleCountryChangeEvent>();
                if (triggerBattle == null) return CardStepResult.Nothing;
                _triggerSeaLocation = triggerBattle.CountryState;
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, AdjacentBuildable(GameSituation.Live).ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD);
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(s => AdjacentBuildable(s).Count > 0), this))
            .WithGuidance("Build an army adjacent to a battled sea space"),
            
            // Build second Army also adjacent to the original battled sea space
            new ResultStep(this, async() => {
                if (_triggerSeaLocation == null) return CardStepResult.Nothing;
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, AdjacentBuildable(GameSituation.Live).ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD);
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(s =>
                _triggerSeaLocation != null && AdjacentBuildable(s).Count > 0), this))
            .WithGuidance("Build another army adjacent to the same sea space"),
        }; 
    }
}