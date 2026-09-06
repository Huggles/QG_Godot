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

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int totalDiscards = 1 + (BlockingUnits.Count == 0 ? 1 : 0);

                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(1, "Decima Flottiglia MAS Frogmen"), Faction));

                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, totalDiscards));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
        };
    }
}