using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesPreyonUnprotectedShipping : EWCardLogic
{
    /// <summary>
    /// The Allied navies in the North Sea — the ones HALVING this card, from 5 discards to 2. It
    /// scores on an absence, so what is worth showing is whatever is currently spoiling it; the
    /// North Sea itself is reported too, so an empty one still reads as "this is at full strength".
    /// </summary>
    private List<UnitState> BlockingUnits =>
        CountryState.ForEnum(Country.NorthSea).Units.Values
            .Select(UnitState.ForId)
            .Where(unit => unit.Type == UnitType.NAVY && unit.FactionTeam == FactionTeam.ALLIES)
            .ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(new List<Country> { Country.NorthSea })
            .Plus(TargetSet.Units(BlockingUnits));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.Fixed(() =>
                new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, BlockingUnits.Count > 0 ? 2 : 5))),
            // A ResultStep, so the VP is a trigger like the scoring step of every other two-event
            // Economic Warfare card. It used to carry IsTrigger = false, alone among the ten, which
            // meant no reaction window opened on the points -- the typed steps made the
            // disagreement visible and this is the side it was resolved on.
            new ResultStep(this, Choose.Fixed(() =>
                new ScorePointsChangeEvent(new VPEntry(1, "Submarines Prey on Unprotected Shipping"), Faction)))
            // Gated like every other two-event EW card: without it ContinueWithNextSteps can hoist
            // this VP into the discard step's own after-reaction window.
            .RequiringPreviousStep()
        }; 
    }
}