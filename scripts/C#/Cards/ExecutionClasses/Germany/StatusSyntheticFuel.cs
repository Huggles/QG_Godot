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
                .SelectMany(countryId => CountryState.ForId(countryId).AdjacentCountryStates(Faction))
                .Distinct()
                .Where(countryState => countryState.CanBuild(Faction) && countryState.IsLand)
                .ToList();
            
        }
    }

    public override List<CardStep> OnActivate() 
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                DebugUtilities.PrintPeer("StatusSyntheticFuel react step");
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 2));
                discardEvent.IsTrigger = false;
                await CardPlayPool.DoChangeEvent(discardEvent);

                int countryId = await new SelectCountryHandler(DeployTargets.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            }).WithGuidance("Deploy an army adjacent to where you've deployed an army this turn")
        };
    }
}
