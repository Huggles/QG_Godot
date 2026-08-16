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
            new CardStep(this, async() => {
                var triggerBattle = CardPlayPool.CurrentReactionTrigger as BattleCountryChangeEvent;
                if (triggerBattle == null) return;
                _triggerSeaLocation = triggerBattle.CountryState;
                var adjacentBuildable = CountryState.BuildableLand(Faction)
                    .Where(cs => _triggerSeaLocation.ConnectedCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, adjacentBuildable.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var triggerBattle = CardPlayPool.CurrentReactionTrigger as BattleCountryChangeEvent;
                if (triggerBattle == null) return false;
                return CountryState.BuildableLand(Faction).Any(cs => triggerBattle.CountryState.ConnectedCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Build an army adjacent to a battled sea space"),
            
            // Build second Army also adjacent to the original battled sea space
            new CardStep(this, async() => {
                if (_triggerSeaLocation == null) return;
                var adjacentBuildable = CountryState.BuildableLand(Faction)
                    .Where(cs => _triggerSeaLocation.ConnectedCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, adjacentBuildable.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                if (_triggerSeaLocation == null) return false;
                return CountryState.BuildableLand(Faction).Any(cs => _triggerSeaLocation.ConnectedCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Build another army adjacent to the same sea space"),
        }; 
    }
}