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
    private List<(Country Home, List<UnitState> Units, Faction AxisFaction)> QualifyingHomes(BoardState board)
    {
        var usUnits = board.ActiveUnits(Faction);
        return AxisHomes
            .Select(pair =>
            {
                var adjacentIds = CountryState.ForEnum(pair.HomeCountry).ConnectedCountryStates
                    .Select(adj => adj.Id).ToHashSet();
                return (
                    Home: pair.HomeCountry,
                    Units: usUnits.Where(us => adjacentIds.Contains(board.CountryOf(us))).ToList(),
                    pair.AxisFaction);
            })
            .Where(entry => entry.Units.Count > 0)
            .ToList();
    }

    private List<Faction> QualifyingAxisFactions(BoardState board) =>
        QualifyingHomes(board).Select(entry => entry.AxisFaction).ToList();

    /// <summary>Every home this card could reach, and the pieces putting them in range. These are the
    /// candidates the selection modal offers; only the one the player picks is actually hit.</summary>
    public override TargetSet Targets()
    {
        var qualifying = QualifyingHomes(BoardState.Live);
        return TargetSet.Countries(qualifying.Select(entry => entry.Home).ToList())
            .Plus(TargetSet.Units(qualifying.SelectMany(entry => entry.Units).ToList()));
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            // One target, not every qualifier: the card text's "that country" is singular, so the
            // player chooses which reachable Axis power takes the hit.
            new ResultStep(this, Choose.FactionFrom(c => QualifyingAxisFactions(c.Board),
                (targetFaction, _) => new ForceDiscardCardsChangeEvent(Faction, targetFaction, 7)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => QualifyingAxisFactions(s.Board).Count > 0), this))
            .WithGuidance("Choose an Axis country with a US unit adjacent to its Home space to discard 7 cards")
        };
    }
}