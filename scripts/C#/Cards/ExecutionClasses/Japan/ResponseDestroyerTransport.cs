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

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep> {
            // Build first Army adjacent to battled space
            new CardStep(this, async() => {
                var battleEvents = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
                    .Where(ce => ce.TriggeringFaction == Faction).ToList();
                var battleLocation = battleEvents.Last().CountryState;
                var adjacentBuildable = CountryState.BuildableLand(Faction)
                    .Where(cs => battleLocation.NeighborCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = await new SelectCountryHandler(adjacentBuildable.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var battleEvents = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
                    .Where(ce => ce.TriggeringFaction == Faction && ce.CountryState.Type == CountryType.SEA).ToList();
                if (!battleEvents.Any()) return false;
                var battleLocation = battleEvents.Last().CountryState;
                return CountryState.BuildableLand(Faction).Any(cs => battleLocation.NeighborCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Build an army adjacent to the battled space"),
            
            // Build second Army adjacent to battled space (optional)
            new CardStep(this, async() => {
                var battleEvents = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
                    .Where(ce => ce.TriggeringFaction == Faction).ToList();
                var battleLocation = battleEvents.First(ce => ce.CountryState.Type == CountryType.SEA).CountryState;
                var adjacentBuildable = CountryState.BuildableLand(Faction)
                    .Where(cs => battleLocation.NeighborCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = await new SelectCountryHandler(adjacentBuildable.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var battleEvents = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
                    .Where(ce => ce.TriggeringFaction == Faction && ce.CountryState.Type == CountryType.SEA).ToList();
                if (!battleEvents.Any()) return false;
                var battleLocation = battleEvents.Last().CountryState;
                return CountryState.BuildableLand(Faction).Any(cs => battleLocation.NeighborCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Build another army adjacent to the battled space"),
        }; 
    }
}