using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BuildNavy : CardLogic
{
    /// <summary>
    /// The one definition of what this card can hit, read by both the step's selection and
    /// <see cref="Targets"/> so the hover preview cannot drift from the real offer.
    /// </summary>
    private List<int> BuildTargets => CountryState.BuildableSea(Faction).ToCountryIds();

    public override TargetSet Targets() => TargetSet.Countries(BuildTargets);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, BuildTargets).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.HasBuildableSea(Faction), this))
            .WithAdvisoryConditions(()=> new List<Condition> {
                Condition.Build(new Condition.HasVacantBuildableSea(Faction), this),
                Condition.Build(new Condition.HasAvailableUnits(Faction, UnitType.NAVY), this)
            })
            .WithGuidance("Build a navy")
        }; 
    }
}
