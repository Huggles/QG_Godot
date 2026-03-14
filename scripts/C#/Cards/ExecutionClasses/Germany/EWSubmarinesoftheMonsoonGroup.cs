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
                Variant[] response = await PresentationModal.Instance.ShowModal(
                    PresentationItemImageButton.ForFactions([Faction.UNITED_KINGDOM, Faction.UNITED_STATES, Faction.SOVIET]), 
                    "Select Allied country");
                Faction selectedFaction = (Faction)response[0].As<int>();
                await PresentationModal.Instance.HideModal();
                
                // Selected faction discards 2 cards
                DiscardCardsChangeEvent discardEvent = BuildChangeEvent(new DiscardCardsChangeEvent(Faction, selectedFaction, 2));
                discardEvent.IsTrigger = true;
                
                // Score 2 VP
                IVictoryStepHandler vpHandler = GameSession.Instance.GameFlow.vpStepHandler;
                await vpHandler.ScorePoints(new VPEntry(2, "2 VP for Submarines of the Monsoon Group."));
                
                return discardEvent;
            })
        }; 
    }
}