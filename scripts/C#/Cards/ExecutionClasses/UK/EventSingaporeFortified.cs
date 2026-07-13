using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventSingaporeFortified : EventCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Recruit an Army in Southeast Asia
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)Country.SouthEastAsia]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)Country.SouthEastAsia], Faction), this))
            .WithGuidance("Recruit an army in Southeast Asia"),
            
            // Recruit a Navy in South China Sea
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)Country.SouthChinaSea]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)Country.SouthChinaSea], Faction), this))
            .WithGuidance("Recruit a navy in the South China Sea"),
        }; 
    }
}