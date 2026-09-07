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

    /// <summary>The Axis homes in reach, paired with the US Armies putting them there. One pass,
    /// read by the faction list, the condition and <see cref="Targets"/> alike.</summary>
    private List<(Country Home, List<UnitState> Armies, Faction AxisFaction)> QualifyingHomes() =>
        AxisHomes
            .Select(pair => (
                Home: pair.HomeCountry,
                Armies: FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                    .Where(us => us.Type == UnitType.ARMY)
                    .Where(us => PathFindingService.IsWithinGeographicDistance(us.CountryId, (int)pair.HomeCountry, 3))
                    .ToList(),
                pair.AxisFaction))
            .Where(entry => entry.Armies.Count > 0)
            .ToList();

    private List<Faction> QualifyingAxisFactions() =>
        QualifyingHomes().Select(entry => entry.AxisFaction).ToList();

    /// <summary>Every home this card could reach, and the Armies putting them in range — which is
    /// what the card text leaves you to work out for yourself. These are the candidates the
    /// selection modal offers; only the one the player picks is actually hit.</summary>
    public override TargetSet Targets()
    {
        var qualifying = QualifyingHomes();
        return TargetSet.Countries(qualifying.Select(entry => entry.Home).ToList())
            .Plus(TargetSet.Units(qualifying.SelectMany(entry => entry.Armies).ToList()));
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // One target, not every qualifier: the card text's "that country" is singular, so the
                // player chooses which reachable Axis power takes the hit. The step condition below
                // already keeps the card unplayable while this list is empty, so the modal is never
                // shown without options.
                var factionResp = await new InputRequest.SelectFactionRequestHandler(
                    Faction, QualifyingAxisFactions()).BroadCast();
                Faction targetFaction = (Faction)factionResp.ResponseCardIds[0];

                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, targetFaction, 4));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => QualifyingAxisFactions().Count > 0), this))
            .WithGuidance("Choose an Axis country with a US Army within 3 spaces of its Home to discard 4 cards")
        };
    }
}