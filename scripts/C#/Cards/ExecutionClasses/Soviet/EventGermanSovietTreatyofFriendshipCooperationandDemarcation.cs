using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventGermanSovietTreatyofFriendshipCooperationandDemarcation : EventCardLogic
{
    /// <summary>The two recruit spaces, in step order.</summary>
    private static readonly List<Country> targetCountries = [Country.Russia, Country.EasternEurope];

    public override TargetSet Targets() => TargetSet.Countries(targetCountries);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Recruit an Army in Russia
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)targetCountries[0]]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)targetCountries[0]], Faction), this))
            .WithGuidance("Recruit an army in Russia"),
            
            // Recruit an Army in Eastern Europe
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)targetCountries[1]]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)targetCountries[1]], Faction), this))
            .WithGuidance("Recruit an army in Eastern Europe"),
        }; 
    }
}