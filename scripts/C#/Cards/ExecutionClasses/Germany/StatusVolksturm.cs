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

    public override List<CardStep> InitializeReactCardSteps() 
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                ForceDiscardCardsChangeEvent discardEvent = new ForceDiscardCardsChangeEvent(Faction, Faction, 1);
                discardEvent.IsTrigger = false;
                await discardEvent.ApplyChange();

                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(discardEvent.DiscardedCardIds, false);
                await PresentationModal.Current.ShowModal(presentationItems, "Discarded cards");          
                
                int selectedCountryId = await new SelectCountryHandler(new List<int>{(int)Country.Germany}).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            })
            .WithGuidance("Recruit an army in Germany (in addition to your playstep)")
        };
    }
}
