using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusBlitzkrieg : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.FactionBattled(Faction), this),
            Condition.Build(new Condition.CountryIsEmpty(CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>().Map(changeEvent=> changeEvent.CountryId)),this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 1));
                discardEvent.IsTrigger = false;
                await CardPlayPool.DoChangeEvent(discardEvent);
                                
                List<BattleCountryChangeEvent> changeEvents = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>().Where(changeEvent=>changeEvent.CountryState.Units.Count == 0).ToList();
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, changeEvents.Map(changeEvent => changeEvent.CountryId)).BroadCast()).ResponseCountryIds[0];

                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;                
            }).WithGuidance("Deploy an army in a country where you've battle this turn") 
        };
    }
}
