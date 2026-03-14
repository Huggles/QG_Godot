using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseSpecialNavalLandingForces : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { 
            Condition.Build(new Condition.HasDeployedNavy(Faction), this) 
        };
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep> {
            // Build first Army adjacent to built Navy
            new CardStep(this, async() => {
                var deployEvents = CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()
                    .Where(ce => ce.TriggeringFaction == Faction && 
                                ce.DeploymentType == DeployType.BUILD && 
                                ce.UnitType == UnitType.NAVY).ToList();
                var builtLocation = deployEvents.Last().CountryState;
                var adjacentBuildable = CountryState.BuildableLand(Faction)
                    .Where(cs => builtLocation.NeighborCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = await new SelectCountryHandler(adjacentBuildable.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var deployEvents = CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()
                    .Where(ce => ce.TriggeringFaction == Faction && 
                                ce.DeploymentType == DeployType.BUILD && 
                                ce.UnitType == UnitType.NAVY).ToList();
                if (!deployEvents.Any()) return false;
                var builtLocation = deployEvents.Last().CountryState;
                return CountryState.BuildableLand(Faction).Any(cs => builtLocation.NeighborCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Build an army adjacent to the navy just built"),
            
            // Build second Army adjacent to built Navy
            new CardStep(this, async() => {
                var deployEvents = CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()
                    .Where(ce => ce.TriggeringFaction == Faction && 
                                ce.DeploymentType == DeployType.BUILD && 
                                ce.UnitType == UnitType.NAVY).ToList();
                var builtLocation = deployEvents.Last().CountryState;
                var adjacentBuildable = CountryState.BuildableLand(Faction)
                    .Where(cs => builtLocation.NeighborCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = await new SelectCountryHandler(adjacentBuildable.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var deployEvents = CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()
                    .Where(ce => ce.TriggeringFaction == Faction && 
                                ce.DeploymentType == DeployType.BUILD && 
                                ce.UnitType == UnitType.NAVY).ToList();
                if (!deployEvents.Any()) return false;
                var builtLocation = deployEvents.Last().CountryState;
                return CountryState.BuildableLand(Faction).Any(cs => builtLocation.NeighborCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Build second army adjacent to the navy just built"),
        }; 
    }
}