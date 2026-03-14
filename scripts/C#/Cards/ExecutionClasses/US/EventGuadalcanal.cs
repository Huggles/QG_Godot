using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventGuadalcanal : EventCardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep> {
            // Recruit an Army in New Zealand
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler([(int)Country.NewZealand]).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)Country.NewZealand], Faction), this))
            .WithGuidance("Recruit an army in New Zealand"),
            
            // Build a Navy adjacent to New Zealand
            new CardStep(this, async() => {
                var newZealand = CountryState.ForEnum(Country.NewZealand);
                var adjacentBuildableNavies = CountryState.BuildableSea(Faction)
                    .Where(cs => newZealand.NeighborCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = await new SelectCountryHandler(adjacentBuildableNavies.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var newZealand = CountryState.ForEnum(Country.NewZealand);
                return CountryState.BuildableSea(Faction).Any(cs => newZealand.NeighborCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Build a navy adjacent to New Zealand"),
        }; 
    }
}