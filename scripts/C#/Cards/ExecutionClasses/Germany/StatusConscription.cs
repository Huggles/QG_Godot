using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusConscription : StatusCardLogic
{
    public List<int> BuildableLandCountries()
    {
        return CountryState.BuildableLand(Faction).ToCountryIds();
    }    
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsPlayCardStep(), this)
        };
    }

    public override List<CardStep> OnActivate() 
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = new ForceDiscardCardsChangeEvent(Faction, Faction, 2);
                discardEvent.IsTrigger = false;
                await discardEvent.ApplyChange();

                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(discardEvent.DiscardedCardIds, false);
                await PresentationModal.Current.ShowModal(presentationItems, "Discarded cards");
                
                int selectedCountryId = await new SelectCountryHandler(BuildableLandCountries()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsBuildable(BuildableLandCountries(), Faction),this))            
            .WithGuidance("Build an army")
        };
    }
}
