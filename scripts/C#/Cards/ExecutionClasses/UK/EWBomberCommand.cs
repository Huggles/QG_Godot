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

                Variant[] response = await PresentationModal.Current.ShowModalPersistent(PresentationItemImageButton.ForFactions([Faction.GERMANY,Faction.ITALY]), "Select a faction");                
                Faction selectedFaction = (Faction)response[0].As<int>();
                await PresentationModal.Current.HideModal();

                ForceDiscardCardsChangeEvent ForceDiscardCardsChangeEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, selectedFaction, 4));
                ForceDiscardCardsChangeEvent.IsTrigger = true;
                return ForceDiscardCardsChangeEvent;      
            })
        };
    }
}