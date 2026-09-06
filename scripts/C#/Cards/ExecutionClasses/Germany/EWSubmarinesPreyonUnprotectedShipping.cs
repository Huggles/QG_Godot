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
            new CardStep(this, async() => {
                int discardCount = BlockingUnits.Count > 0 ? 2 : 5;
                
                // UK discards cards
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, discardCount));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            }),
            new CardStep(this, async() => {               
                // Score 1 VP       
                ScorePointsChangeEvent scorePointsEvent = BuildChangeEvent(new ScorePointsChangeEvent(new VPEntry(1, "Submarines Prey on Unprotected Shipping"), Faction));                
                scorePointsEvent.IsTrigger = false;
                await CardPlayPool.DoChangeEvent(scorePointsEvent);
            })
        }; 
    }
}