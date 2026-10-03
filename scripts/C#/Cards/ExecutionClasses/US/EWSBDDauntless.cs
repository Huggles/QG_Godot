using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSBDDauntless : EWCardLogic
{
    /// <summary>
    /// The US Navies close enough to Japan to arm this card. Reuses the same
    /// PathFindingService range test the condition always did — extracted so <see cref="Targets"/>
    /// can show WHICH piece is arming it, which is the one thing the card text does not tell you.
    /// </summary>
    private List<UnitState> QualifyingUnits(BoardState board) =>
        board.ActiveUnits(Faction)
            .Where(us => us.Type == UnitType.NAVY)
            .Where(us => PathFindingService.IsWithinGeographicDistance(board.CountryOf(us), (int)Country.Japan, 2))
            .ToList();

    private bool QualifyingUnitExists(BoardState board) => QualifyingUnits(board).Count > 0;

    /// <summary>The pieces arming this card, and the home space they are bombing.</summary>
    public override TargetSet Targets() =>
        TargetSet.Units(QualifyingUnits(BoardState.Live))
            .Plus(TargetSet.Countries(new List<Country> { Country.Japan }));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction.JAPAN, 4)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => QualifyingUnitExists(s.Board)), this))
            .WithGuidance("Make Japan discard 4 cards")
        };
    }
}