using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseDestroyerTransport : ResponseCardLogic
{
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
                if (triggerBattle == null) return null;
                var battleLocation = triggerBattle.CountryState;
                var adjacentBuildable = CountryState.BuildableLand(Faction)
                    .Where(cs => battleLocation.ConnectedCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, adjacentBuildable.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var triggerBattle = CardPlayPool.CurrentReactionTrigger as BattleCountryChangeEvent;
                if (triggerBattle == null) return false;
                return CountryState.BuildableLand(Faction).Any(cs => triggerBattle.CountryState.ConnectedCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Build an army adjacent to a battled sea space"),
            
            // Build second Army adjacent to the sea spaces adjacent to the first army built
            new CardStep(this, async() => {
                var firstArmyLocation = CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()
                    .Last(ce => ce.SourceCardId == CardState.Id).CountryState;
                var adjacentSeas = firstArmyLocation.ConnectedCountryStates
                    .Where(cs => cs.Type == CountryType.SEA).ToList();
                var adjacentBuildable = CountryState.BuildableLand(Faction)
                    .Where(cs => adjacentSeas.Any(sea => sea.ConnectedCountryStates.Contains(cs)))
                    .ToList();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, adjacentBuildable.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var firstArmyDeploys = CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()
                    .Where(ce => ce.SourceCardId == CardState.Id).ToList();
                if (!firstArmyDeploys.Any()) return false;
                var firstArmyLocation = firstArmyDeploys.Last().CountryState;
                var adjacentSeas = firstArmyLocation.ConnectedCountryStates
                    .Where(cs => cs.Type == CountryType.SEA).ToList();
                return CountryState.BuildableLand(Faction)
                    .Any(cs => adjacentSeas.Any(sea => sea.ConnectedCountryStates.Contains(cs)));
            }), this))
            .WithGuidance("Build another army adjacent to the same sea space"),
        }; 
    }
}