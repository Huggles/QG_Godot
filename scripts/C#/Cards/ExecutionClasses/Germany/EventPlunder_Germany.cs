using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventPlunder_Germany : EventCardLogic
{
    /// <summary>
    /// The pieces that score. Read by both the step and <see cref="Targets"/>, so hovering the card
    /// lights exactly the units it is about to count.
    /// </summary>
    private List<UnitState> ScoringUnits(BoardState board) =>
        board.ActiveUnits(Faction)
            .Where(unitState => board.CountryStateOf(unitState).Country != Country.Germany)
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits(BoardState.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.Fixed(c => {
                FactionState factionState = FactionState.ForEnum(Faction);
                int score = ScoringUnits(c.Board).Count;
                return new ScorePointsChangeEvent(new VPEntry(score, $"{factionState.FactionData.FactionAdjactiveLabel} armies and navies outside Germany"), Faction);
            }))
        }; 
    }
}