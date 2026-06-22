using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventGermanSovietTreatyofFriendshipCooperationandDemarcation : EventCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Recruit an Army in Russia
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler([(int)Country.Russia]).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Russia], Faction), this))
            .WithGuidance("Recruit an army in Russia"),
            
            // Recruit an Army in Eastern Europe
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler([(int)Country.EasternEurope]).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)Country.EasternEurope], Faction), this))
            .WithGuidance("Recruit an army in Eastern Europe"),
        }; 
    }
}