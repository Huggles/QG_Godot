using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWFirestormBombing : EWCardLogic
{
    private static readonly List<(Country HomeCountry, Faction AxisFaction)> AxisHomes =
    [
        (Country.Germany, Faction.GERMANY),
        (Country.Japan,   Faction.JAPAN),
        (Country.Italy,   Faction.ITALY)
    ];

    private List<Faction> QualifyingAxisFactions()
    {
        var usUnitCountryIds = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Select(us => us.CountryId).ToHashSet();
        return AxisHomes
            .Where(pair => CountryState.ForEnum(pair.HomeCountry).ConnectedCountryStates
                .Any(adj => usUnitCountryIds.Contains(adj.Id)))
            .Select(pair => pair.AxisFaction)
            .ToList();
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                foreach (Faction targetFaction in QualifyingAxisFactions())
                {
                    ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, targetFaction, 7));
                    discardEvent.IsTrigger = true;
                    await CardPlayPool.DoChangeEvent(discardEvent);
                }
                return null;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => QualifyingAxisFactions().Count > 0), this))
            .WithGuidance("Axis country with US unit adjacent to its Home space must discard 7 cards")
        };
    }
}