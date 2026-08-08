using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesLeadtheBattleoftheAtlantic : EWCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // Count all German Navies on the board
                var germanNavies = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                    .Where(u => u.Type == UnitType.NAVY)
                    .ToList();
                
                int count = germanNavies.Count;

                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(count, "German Navies on the board"), Faction));
                
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, count * 2));
                discardEvent.IsTrigger = true;
                return discardEvent;
            })
        }; 
    }
}