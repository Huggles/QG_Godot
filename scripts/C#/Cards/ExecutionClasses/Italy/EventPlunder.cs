using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventPlunder : EventCardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this,async() => {
                FactionState factionState = FactionState.ForEnum(Faction);
                int score = factionState.ActiveUnitIds.ToUnitStates().ToList().Count(unitState => unitState.CountryState.Country != Country.Italy);
                IVictoryStepHandler iVPStepHandler = GameSession.Instance.GameFlow.vpStepHandler;
                await iVPStepHandler.ScorePoints(new VPEntry(score,$"{score} VPs for {factionState.FactionData.FactionAdjactiveLabel} armies and navies outside italy."));                
            })
        }; 
    }
}