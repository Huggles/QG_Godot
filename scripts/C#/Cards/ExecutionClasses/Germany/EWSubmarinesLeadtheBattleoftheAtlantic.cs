using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesLeadtheBattleoftheAtlantic : EWCardLogic
{
    /// <summary>Every German Navy on the board: one VP and two UK discards each. Read by both the
    /// step and <see cref="Targets"/>, so hovering shows exactly what this card is worth.</summary>
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

                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(count, "German Navies on the board"), Faction));
                
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, count * 2));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
        }; 
    }
}