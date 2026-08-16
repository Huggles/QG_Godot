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
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this)
        };
    }

    public override List<CardStep> OnActivate() 
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                SpendPlayActionChangeEvent spendEvent = BuildChangeEvent(new SpendPlayActionChangeEvent(Faction));
                spendEvent.IsTrigger = false;
                await spendEvent.Apply();

                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 2));
                discardEvent.IsTrigger = false;
                await discardEvent.Apply();

                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, BuildableLandCountries()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsBuildable(BuildableLandCountries(), Faction),this))            
            .WithGuidance("Build an army")
        };
    }
}
