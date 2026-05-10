using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusSyntheticFuel : StatusCardLogic
{    
    protected override List<Condition> CardTriggers()
    {        
        return new List<Condition> {
            Condition.Build(new Condition.FactionDeployed(Faction, DeployType.BUILD), this),
            Condition.Build(
                new Condition.CountryIsBuildable(
                    DeployTargets.ToCountryIds(),
                    Faction),
                this
            )
            
        };
    }
    
    public List<CountryState> DeployTargets
    {
        get 
        {
            return CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()
                .Map(changeEvent => changeEvent.CountryId)
                .SelectMany(countryId => CountryState.ForId(countryId).ConnectedCountries(Faction))
                .Distinct()
                .Where(countryState => countryState.CanBuild(Faction) && countryState.IsLand)
                .ToList();
            
        }
    }

    public override List<CardStep> InitializeReactCardSteps() 
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(DeckState.ForFaction(Faction).DiscardTopCards(2), false);
                await PresentationModal.Instance.ShowModal(presentationItems, "Discarded cards");

                int countryId = await new SelectCountryHandler(DeployTargets.ToCountryIds()).Handle();                
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;                
            }).WithGuidance("Deploy an army adjacent to where you've deployed an army this turn") 
        };
    }
}
