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

                IVictoryStepHandler vpHandler = GameFlow.Instance.vpStepHandler;
                await vpHandler.ScorePoints(new VPEntry(count, $"{count} VP for Italian Navies on the board."));

                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, count));
                discardEvent.IsTrigger = true;
                return discardEvent;
            })
        };
    }
}