using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesLeadtheBattleoftheAtlantic : EWCardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // Count all German Navies on the board
                var germanNavies = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                    .Where(u => u.Type == UnitType.NAVY)
                    .ToList();
                
                int count = germanNavies.Count;
                
                // Score VP
                if (count > 0)
                {
                    IVictoryStepHandler vpHandler = GameSession.Instance.GameFlow.vpStepHandler;
                    await vpHandler.ScorePoints(new VPEntry(count, $"{count} VP for German Navies on the board."));
                }
                
                // UK discards 2 cards per navy
                DiscardCardsChangeEvent discardEvent = BuildChangeEvent(new DiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, count * 2));
                discardEvent.IsTrigger = true;
                return discardEvent;
            })
        }; 
    }
}