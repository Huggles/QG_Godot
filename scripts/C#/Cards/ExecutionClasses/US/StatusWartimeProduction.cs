using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusWartimeProduction : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasDeployedArmy(Faction), this).Immediately(),
            Condition.Build(new Condition.HasBuildableLand(Faction), this)
        };
    }
    
    public List<int> DeployableCountryIds
    {
        get
        {
            return CountryState.BuildableLand(Faction).Select(cs => cs.Id).ToList();
        }
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 1));
                discardEvent.IsTrigger = false;
                await discardEvent.ApplyChange();

                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(discardEvent.DiscardedCardIds, false);
                await PresentationServices.Notification.ShowModal(presentationItems, "Discarded cards");

                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, DeployableCountryIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;                
            }).WithGuidance("Discard top 1 deck card to build an additional Army")
        };
    }


}