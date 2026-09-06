using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventSingaporeFortified : EventCardLogic
{
    /// <summary>The army space and the navy space, in step order.</summary>
    private static readonly List<Country> targetCountries = [Country.SouthEastAsia, Country.SouthChinaSea];

    public override TargetSet Targets() => TargetSet.Countries(targetCountries);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Recruit an Army in Southeast Asia
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)targetCountries[0]]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)targetCountries[0]], Faction), this))
            .WithGuidance("Recruit an army in Southeast Asia"),
            
            // Recruit a Navy in South China Sea
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)targetCountries[1]]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)targetCountries[1]], Faction), this))
            .WithGuidance("Recruit a navy in the South China Sea"),
        }; 
    }
}