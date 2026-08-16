using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWRegiaMarinaClosesShippingLanes : EWCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                var italianNavies = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                    .Where(u => u.Type == UnitType.NAVY)
                    .ToList();

                int count = italianNavies.Count;

                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(count, "Italian Navies on the board"), Faction));

                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, count));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
        };
    }
}