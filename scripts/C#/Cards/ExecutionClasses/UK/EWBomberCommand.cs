using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

public partial class EWBomberCommand : EWCardLogic
{
    
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {

                Variant[] response = await PresentationModal.Instance.ShowModal(PresentationItemImageButton.ForFactions([Faction.GERMANY,Faction.ITALY]), "Select a faction");                
                Faction selectedFaction = (Faction)response[0].As<int>();
                await PresentationModal.Instance.HideModal();

                DiscardCardsChangeEvent discardCardsChangeEvent = BuildChangeEvent(new DiscardCardsChangeEvent(Faction, selectedFaction, 4));
                discardCardsChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardCardsChangeEvent);                
            })
        };
    }
}