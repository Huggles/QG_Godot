using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventGuadalcanal : EventCardLogic
{
    private static readonly List<int> anchorCountryIds = [(int)Country.NewZealand];

    /// <summary>The sea spaces the navy may actually be built in — the same filtered list step 2
    /// offers, so the preview and the offer cannot disagree.</summary>
    private List<CountryState> AdjacentNavyTargets =>
        CountryState.BuildableSea(Faction)
            .Where(cs => CountryState.ForEnum(Country.NewZealand).ConnectedCountryStates.Contains(cs))
            .ToList();

    /// <summary>New Zealand for the army, and the sea spaces beside it for the navy.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(anchorCountryIds).Plus(TargetSet.Countries(AdjacentNavyTargets));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Recruit an Army in New Zealand
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, anchorCountryIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable(anchorCountryIds, Faction), this))
            .WithGuidance("Recruit an army in New Zealand"),
            
            // Build a Navy adjacent to New Zealand
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, AdjacentNavyTargets.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                return AdjacentNavyTargets.Count > 0;
            }), this))
            .WithGuidance("Build a navy adjacent to New Zealand"),
        }; 
    }
}