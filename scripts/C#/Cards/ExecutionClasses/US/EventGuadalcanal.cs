using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventGuadalcanal : EventCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Recruit an Army in New Zealand
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)Country.NewZealand]).BroadCast()).ResponseCountryIds[0];
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
                    .Where(cs => newZealand.ConnectedCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, adjacentBuildableNavies.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var newZealand = CountryState.ForEnum(Country.NewZealand);
                return CountryState.BuildableSea(Faction).Any(cs => newZealand.ConnectedCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Build a navy adjacent to New Zealand"),
        }; 
    }
}