using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventPlunder_Italy : EventCardLogic
{
    /// <summary>
    /// The pieces that score. Read by both the step and <see cref="Targets"/>, so hovering the card
    /// lights exactly the units it is about to count.
    /// </summary>
    private List<UnitState> ScoringUnits =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(unitState => unitState.CountryState.Country != Country.Italy)
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits);

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this,async() => {
                FactionState factionState = FactionState.ForEnum(Faction);
                int score = ScoringUnits.Count;
                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(score, $"{factionState.FactionData.FactionAdjactiveLabel} armies and navies outside Italy"), Faction));
            })
        }; 
    }
}