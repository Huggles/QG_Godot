using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventFleetDeployedtoPearlHarbor : EventCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)Country.Hawaii]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Hawaii], Faction), this))
            .WithGuidance("Recruit an Army in Hawaii"),
            new CardStep(this, async() => {
                var hawaii = CountryState.ForEnum(Country.Hawaii);
                var adjacentBuildableSea = CountryState.BuildableSea(Faction)
                    .Where(cs => hawaii.ConnectedCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, adjacentBuildableSea.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => {
                var hawaii = CountryState.ForEnum(Country.Hawaii);
                return CountryState.BuildableSea(Faction).Any(cs => hawaii.ConnectedCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Build a Navy adjacent to Hawaii"),
        };
    }
}