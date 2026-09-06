using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWVWeapons : EWCardLogic
{
    /// <summary>The German Armies in Western Europe. The card pays a flat 3 VP if there is at least
    /// one, so the preview shows what is unlocking it — and Western Europe itself, so an empty board
    /// still says where to look.</summary>
    private List<UnitState> ScoringUnits =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(u => u.Type == UnitType.ARMY && u.CountryState.Country == Country.WesternEurope)
            .ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(new List<Country> { Country.WesternEurope })
            .Plus(TargetSet.Units(ScoringUnits));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                if (ScoringUnits.Count > 0)
                {
                    // Score 3 VP
                    await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(3, "German Army in Western Europe"), Faction));
                    
                    // UK discards 1 card
                    ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, 1));
                    discardEvent.IsTrigger = true;
                    await CardPlayPool.DoChangeEvent(discardEvent);
                }
            })
        }; 
    }
}