using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusConscription : StatusCardLogic
{
    public List<int> BuildableLandCountries()
    {
        return DeployState.CalculateDeployState(Faction).BuildableCountryStatesForType(CountryType.LAND).ToCountryIds();
    }    
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsGameFlowStep(TurnStep.PLAY_CARD), this)
        };
    }

    public override List<CardStep> InitializeReactCardSteps() 
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(DeckState.ForFaction(Faction).DiscardTopCards(2), false);
                await PresentationModal.Instance.ShowModal(presentationItems, "Discarded cards", 2000);
                
                int selectedCountryId = await new SelectCountryHandler(BuildableLandCountries()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                _ = CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsBuildable(BuildableLandCountries(), Faction),this))            
            .WithGuidance("Build an army")
        };
    }
}
