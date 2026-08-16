using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventPlunder_Italy : EventCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this,async() => {
                FactionState factionState = FactionState.ForEnum(Faction);
                int score = factionState.ActiveUnitIds.ToUnitStates().ToList().Count(unitState => unitState.CountryState.Country != Country.Italy);
                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(score, $"{factionState.FactionData.FactionAdjactiveLabel} armies and navies outside Italy"), Faction));
            })
        }; 
    }
}