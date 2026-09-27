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
    private List<UnitState> BlockingUnits =>
        StaticGameData.FactionsForTeam(FactionTeam.ALLIES)
            .SelectMany(faction => FactionState.ForEnum(faction).ActiveUnitIds.ToUnitStates())
            .Where(us => us.Type == UnitType.NAVY
                      && us.CountryState.Country == Country.MediterraneanSea)
            .ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(new List<Country> { Country.MediterraneanSea })
            .Plus(TargetSet.Units(BlockingUnits));

    /// <summary>
    /// Captured by the scoring step and read by the discard step, rather than recomputed. The two
    /// are separate steps now, and a reaction played in the scoring step's after-reaction window can
    /// move the board between them -- recomputing would make the discard disagree with the VP that
    /// was actually awarded.
    /// </summary>
    private int _totalDiscards;

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.Fixed(() =>
                new ScorePointsChangeEvent(new VPEntry(1, "Decima Flottiglia MAS Frogmen"), Faction))
            .OnChosen(_ => _totalDiscards = 1 + (BlockingUnits.Count == 0 ? 1 : 0))),

            new ResultStep(this, Choose.Fixed(() =>
                new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, _totalDiscards)))
            .RequiringPreviousStep()
        };
    }
}
