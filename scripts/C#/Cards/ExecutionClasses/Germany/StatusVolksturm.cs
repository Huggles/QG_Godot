using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusVolksturm : StatusCardLogic
{    
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsStartStep(), this),
            Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Germany], Faction), this)
        };
    }

    public override List<CardStep> OnActivate() 
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 1));
                discardEvent.IsTrigger = false;
                // No ShowModal here: ForceDiscardCardsChangeEvent.AfterAnimations already queues a
                // ShowDiscardModalAnimation for the same cards, and Apply awaits the animation
                // queue — so showing it again here displayed the discard modal twice.
                await discardEvent.Apply();

                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, new List<int>{(int)Country.Germany}).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithGuidance("Recruit an army in Germany (in addition to your playstep)")
        };
    }
}
