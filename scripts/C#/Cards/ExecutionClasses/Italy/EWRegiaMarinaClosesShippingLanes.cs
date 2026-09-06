using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWRegiaMarinaClosesShippingLanes : EWCardLogic
{
    /// <summary>Every Italian Navy on the board: one VP and one UK discard each. Read by both the
    /// step and <see cref="Targets"/>.</summary>
    private List<UnitState> ScoringUnits =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(u => u.Type == UnitType.NAVY)
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int count = ScoringUnits.Count;

                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(count, "Italian Navies on the board"), Faction));

                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, count));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
        };
    }
}