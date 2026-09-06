using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventFleetDeployedtoPearlHarbor : EventCardLogic
{
    private static readonly List<int> anchorCountryIds = [(int)Country.Hawaii];

    /// <summary>The sea spaces the navy may actually be built in — the same filtered list step 2
    /// offers, so the preview and the offer cannot disagree.</summary>
    private List<CountryState> AdjacentNavyTargets =>
        CountryState.BuildableSea(Faction)
            .Where(cs => CountryState.ForEnum(Country.Hawaii).ConnectedCountryStates.Contains(cs))
            .ToList();

    /// <summary>Hawaii for the army, and the sea spaces beside it for the navy.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(anchorCountryIds).Plus(TargetSet.Countries(AdjacentNavyTargets));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, anchorCountryIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CountryIsRecruitable(anchorCountryIds, Faction), this))
            .WithGuidance("Recruit an Army in Hawaii"),
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, AdjacentNavyTargets.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => {
                return AdjacentNavyTargets.Count > 0;
            }), this))
            .WithGuidance("Build a Navy adjacent to Hawaii"),
        };
    }
}