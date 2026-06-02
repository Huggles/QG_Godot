using System;
using System.Collections.Generic;
using Godot;

public partial class EWSubmarinesoftheMonsoonGroup : EWCardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // Show modal to select Allied faction
                Variant[] response = await PresentationModal.Current.ShowModal(
                    PresentationItemImageButton.ForFactions([Faction.UNITED_KINGDOM, Faction.UNITED_STATES, Faction.SOVIET]), 
                    "Select Allied country");
                Faction selectedFaction = (Faction)response[0].As<int>();
                await PresentationModal.Current.HideModal();
                
                // Selected faction discards 2 cards
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, selectedFaction, 2));
                discardEvent.IsTrigger = true;
                
                // Score 2 VP
                IVictoryStepHandler vpHandler = GameFlow.Instance.vpStepHandler;
                await vpHandler.ScorePoints(new VPEntry(2, "2 VP for Submarines of the Monsoon Group."));
                
                return discardEvent;
            })
        }; 
    }
}