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

    /// <summary>The Axis homes with a US piece next door, paired with the pieces doing it. One pass,
    /// read by the faction list, the condition and <see cref="Targets"/> alike.</summary>
    private List<(Country Home, List<UnitState> Units, Faction AxisFaction)> QualifyingHomes()
    {
        var usUnits = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates();
        return AxisHomes
            .Select(pair =>
            {
                var adjacentIds = CountryState.ForEnum(pair.HomeCountry).ConnectedCountryStates
                    .Select(adj => adj.Id).ToHashSet();
                return (
                    Home: pair.HomeCountry,
                    Units: usUnits.Where(us => adjacentIds.Contains(us.CountryId)).ToList(),
                    pair.AxisFaction);
            })
            .Where(entry => entry.Units.Count > 0)
            .ToList();
    }

    private List<Faction> QualifyingAxisFactions() =>
        QualifyingHomes().Select(entry => entry.AxisFaction).ToList();

    /// <summary>The homes this card actually reaches, and the pieces putting them in range.</summary>
    public override TargetSet Targets()
    {
        var qualifying = QualifyingHomes();
        return TargetSet.Countries(qualifying.Select(entry => entry.Home).ToList())
            .Plus(TargetSet.Units(qualifying.SelectMany(entry => entry.Units).ToList()));
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
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => QualifyingAxisFactions().Count > 0), this))
            .WithGuidance("Axis country with US unit adjacent to its Home space must discard 7 cards")
        };
    }
}