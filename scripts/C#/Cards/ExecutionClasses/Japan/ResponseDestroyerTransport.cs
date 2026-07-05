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
            Condition.Build(new Condition.HasBattledAtSea(Faction), this) 
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Build first Army adjacent to any battled sea space
            new CardStep(this, async() => {
                var battleLocations = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
                    .Where(ce => ce.TriggeringFaction == Faction && ce.CountryState.Type == CountryType.SEA)
                    .Select(ce => ce.CountryState).Distinct().ToList();
                var adjacentBuildable = CountryState.BuildableLand(Faction)
                    .Where(cs => battleLocations.Any(bl => bl.ConnectedCountryStates.Contains(cs)))
                    .ToList();
                int selectedCountryId = await new SelectCountryHandler(adjacentBuildable.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var battleLocations = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
                    .Where(ce => ce.TriggeringFaction == Faction && ce.CountryState.Type == CountryType.SEA)
                    .Select(ce => ce.CountryState).Distinct().ToList();
                if (!battleLocations.Any()) return false;
                return CountryState.BuildableLand(Faction).Any(cs => battleLocations.Any(bl => bl.ConnectedCountryStates.Contains(cs)));
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
                int selectedCountryId = await new SelectCountryHandler(adjacentBuildable.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
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