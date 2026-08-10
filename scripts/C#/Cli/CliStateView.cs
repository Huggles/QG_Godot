using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Read-only renderings of game state for the inspection commands. Every value comes from an
/// existing accessor — nothing is recomputed here, so the CLI can never disagree with the game.
/// </summary>
public static class CliStateView
{
    private static MultiplayerGameState GameState => MultiplayerSession.Instance?.GameState;

    public static bool Ready => GameState != null && GameFlow.Instance != null;

    public static string Summary()
    {
        GameFlow flow = GameFlow.Instance;
        StringBuilder sb = new();
        sb.AppendLine($"turn {flow.GameTurn}  round {flow.Round}  {flow.CurrentFaction} ({flow.CurrentFactionTeam})  step {flow.TurnStep}");
        sb.AppendLine($"hash {GameState.ComputeHash():X8}");
        foreach (Faction faction in StaticGameData.PlayableFactions)
        {
            DeckState deck = DeckState.ForFaction(faction);
            sb.AppendLine($"  {faction,-15} vp {FactionState.ForEnum(faction).Score,3}   " +
                          $"hand {deck.HandCardIds.Count,2}  deck {deck.DeckCardIds.Count,2}  " +
                          $"discard {deck.DiscardedCardIds.Count,2}  " +
                          $"status {deck.StatusCardIds.Count}  response {deck.ResponseCardIds.Count}");
        }
        return sb.ToString().TrimEnd();
    }

    public static string Score()
    {
        int axis = StaticGameData.FactionsForTeam(FactionTeam.AXIS).Sum(f => FactionState.ForEnum(f).Score);
        int allies = StaticGameData.FactionsForTeam(FactionTeam.ALLIES).Sum(f => FactionState.ForEnum(f).Score);
        StringBuilder sb = new();
        sb.AppendLine($"AXIS {axis}   ALLIES {allies}");
        foreach (Faction faction in StaticGameData.PlayableFactions)
            sb.AppendLine($"  {faction,-15} {FactionState.ForEnum(faction).Score}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>Occupied countries only by default — the full map is 60+ mostly-empty rows.</summary>
    public static string Board(Faction? filter = null)
    {
        IEnumerable<CountryState> countries = GameState.CountryStates
            .Where(c => c.Units.Count > 0)
            .Where(c => filter == null || c.Units.ContainsKey(filter.Value))
            .OrderBy(c => c.Name);

        StringBuilder sb = new();
        foreach (CountryState country in countries)
            sb.AppendLine($"  [{country.Id,3}] {country.Label ?? country.Name,-22} {Occupants(country)}");
        return sb.Length == 0 ? "  (no occupied countries)" : sb.ToString().TrimEnd();
    }

    /// <summary>
    /// CountryState.Units maps faction -> UNIT ID (see GameAPI.DeployUnitToCountry), not a count:
    /// a faction holds at most one unit per country. Render the id and type, never a quantity.
    /// </summary>
    private static string Occupants(CountryState country)
    {
        if (country.Units.Count == 0) return "(empty)";
        return string.Join(", ", country.Units.OrderBy(kv => kv.Key).Select(kv =>
        {
            UnitState unit = UnitState.ForId(kv.Value);
            string supply = unit != null && unit.InSupply ? "" : " unsupplied";
            return $"{kv.Key} {unit?.Type.ToString().ToLowerInvariant() ?? "?"}#{kv.Value}{supply}";
        }));
    }

    public static string Country(string nameOrId)
    {
        CountryState country = Resolve(nameOrId);
        if (country == null) return null;

        StringBuilder sb = new();
        sb.AppendLine($"[{country.Id}] {country.Label ?? country.Name}");
        sb.AppendLine($"  units:     {Occupants(country)}");
        sb.AppendLine($"  full:      {country.IsCountryFull}  ({country.Units.Count}/3 factions)");
        sb.AppendLine($"  neighbours: {string.Join(", ", country.ConnectedCountryStates.Select(n => n.Label ?? n.Name))}");
        return sb.ToString().TrimEnd();
    }

    private static CountryState Resolve(string nameOrId)
        => int.TryParse(nameOrId, out int id) ? CountryState.ForId(id) : CountryState.ForName(nameOrId);

    public static string Cards(Faction faction, string pile)
    {
        DeckState deck = DeckState.ForFaction(faction);
        List<int> ids = pile switch
        {
            "hand"        => deck.HandCardIds,
            "deck"        => deck.DeckCardIds,
            "discard"     => deck.DiscardedCardIds,
            "status"      => deck.StatusCardIds,
            "response"    => deck.ResponseCardIds,
            "activatable" => deck.ActivatableCardIds,
            _             => null,
        };
        if (ids == null) return null;

        if (ids.Count == 0) return $"  ({faction} {pile}: empty)";
        return string.Join("\n", ids.Select(id =>
            $"  [{id,3}] {CardState.ForId(id)?.CardName ?? "?"}"));
    }

    /// <summary>The last n applied messages, using each message's own SummaryText().</summary>
    public static string Log(int count)
    {
        List<GameMessage> messages = GameState.GameMessages;
        if (messages.Count == 0) return "  (no messages yet)";
        return string.Join("\n", messages
            .Skip(Math.Max(0, messages.Count - count))
            .Select(m => $"  [{m.Id,4}] {m.GetType().Name,-30} {m.SummaryText()}"));
    }

    public static string Hash() => $"{GameState.ComputeHash():X8}";
}
