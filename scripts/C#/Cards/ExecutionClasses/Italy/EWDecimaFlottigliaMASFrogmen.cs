using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWDecimaFlottigliaMASFrogmen : EWCardLogic
{
    /// <summary>
    /// The Allied navies in the Mediterranean — the ones costing this card its second discard. It
    /// scores on an absence, so what is worth showing is whatever is currently spoiling it; the sea
    /// itself is reported too, so an empty Mediterranean still reads as "this is at full strength".
    /// </summary>
    private List<UnitState> BlockingUnits(BoardState board) =>
        StaticGameData.FactionsForTeam(FactionTeam.ALLIES)
            .SelectMany(faction => board.ActiveUnits(faction))
            .Where(us => us.Type == UnitType.NAVY
                      && board.CountryStateOf(us).Country == Country.MediterraneanSea)
            .ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(new List<Country> { Country.MediterraneanSea })
            .Plus(TargetSet.Units(BlockingUnits(BoardState.Live)));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            // Discard first: it is the only half that depends on the board, so it reads it when it runs.
            new ResultStep(this, Choose.Fixed(c =>
                new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, 1 + (BlockingUnits(c.Board).Count == 0 ? 1 : 0)))),

            new ResultStep(this, Choose.Fixed(_ =>
                new ScorePointsChangeEvent(new VPEntry(1, "Decima Flottiglia MAS Frogmen"), Faction)))
            .RequiringPreviousStep()
        };
    }
}
