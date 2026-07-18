using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWB26Marauder : EWCardLogic
{
    private static readonly List<(Country HomeCountry, Faction AxisFaction)> AxisHomes =
    [
        (Country.Germany, Faction.GERMANY),
        (Country.Japan,   Faction.JAPAN),
        (Country.Italy,   Faction.ITALY)
    ];

    private List<Faction> QualifyingAxisFactions() =>
        AxisHomes
            .Where(pair => FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                .Where(us => us.Type == UnitType.ARMY)
                .Any(us => PathFindingService.IsWithinGeographicDistance(us.CountryId, (int)pair.HomeCountry, 3)))
            .Select(pair => pair.AxisFaction)
            .ToList();

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                foreach (Faction targetFaction in QualifyingAxisFactions())
                {
                    ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, targetFaction, 4));
                    discardEvent.IsTrigger = true;
                    await CardPlayPool.DoChangeEvent(discardEvent);
                }
                return null;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => QualifyingAxisFactions().Count > 0), this))
            .WithGuidance("Axis country with US Army within 3 spaces of its Home must discard 4 cards")
        };
    }
}