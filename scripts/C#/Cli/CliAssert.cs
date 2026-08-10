using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The assertion vocabulary that turns a command script into a regression test.
///
/// Every assertion reads an existing accessor — nothing is recomputed here, so an assertion can
/// never disagree with the game about what the state is. Each returns null on pass, or a
/// human-readable failure.
/// </summary>
public static class CliAssert
{
    /// <summary>Assertions evaluated and failed this run — drives the process exit code.</summary>
    public static int Evaluated { get; private set; }
    public static int Failed { get; private set; }

    public static void Reset() { Evaluated = 0; Failed = 0; }

    /// <summary>
    /// <c>assert &lt;subject&gt; [args…] &lt;op&gt; &lt;expected&gt;</c>. Returns null on pass.
    /// A malformed assertion is reported distinctly from a failed one: a typo in a test is not a
    /// regression in the game, and conflating them wastes debugging time.
    /// </summary>
    public static string Evaluate(List<string> args, out bool malformed)
    {
        malformed = false;
        if (args.Count < 3) { malformed = true; return "usage: assert <subject> [args] <op> <expected>"; }

        // The operator is the last token that looks like one; everything before is the subject.
        int opIndex = args.FindLastIndex(IsOperator);
        if (opIndex <= 0 || opIndex == args.Count - 1)
        {
            malformed = true;
            return $"could not find an operator in: assert {string.Join(" ", args)}";
        }

        List<string> subject = args.Take(opIndex).ToList();
        string op = args[opIndex];
        string expected = string.Join(" ", args.Skip(opIndex + 1));

        string actual;
        try
        {
            actual = Read(subject);
        }
        catch (Exception e)
        {
            malformed = true;
            return e.Message;
        }
        if (actual == null) { malformed = true; return $"unknown assertion subject '{string.Join(" ", subject)}'"; }

        Evaluated++;
        if (Compare(actual, op, expected)) return null;

        Failed++;
        return $"assert {string.Join(" ", subject)} {op} {expected}  —  actual: {actual}";
    }

    private static bool IsOperator(string token)
        => token is "==" or "!=" or ">" or ">=" or "<" or "<=";

    private static string Read(List<string> subject)
    {
        GameFlow flow = GameFlow.Instance;
        MultiplayerGameState state = MultiplayerSession.Instance.GameState;
        string head = subject[0].ToLowerInvariant();
        string arg1 = subject.Count > 1 ? subject[1] : null;

        switch (head)
        {
            case "turn":    return flow.GameTurn.ToString();
            case "round":   return flow.Round.ToString();
            case "step":    return flow.TurnStep.ToString();
            case "faction": return flow.CurrentFaction.ToString();
            case "hash":    return $"{state.ComputeHash():X8}";
            case "errors":  return (CliSession.Instance?.ErrorsSeen ?? 0).ToString();
            case "rngdraws":return GameRandom.DrawCount.ToString();
            case "seed":    return GameRandom.Seed.ToString();

            case "score":   return FactionState.ForEnum(ParseFaction(arg1)).Score.ToString();

            case "vp":
            {
                FactionTeam team = ParseEnum<FactionTeam>(arg1, "team");
                return StaticGameData.FactionsForTeam(team).Sum(f => FactionState.ForEnum(f).Score).ToString();
            }

            // CountryState.Units maps faction -> unit id, so "how many units" is the number of
            // occupying factions, and a per-faction query is presence (0 or 1).
            case "units":
            {
                CountryState country = ParseCountry(arg1);
                if (subject.Count > 2)
                    return country.Units.ContainsKey(ParseFaction(subject[2])) ? "1" : "0";
                return country.Units.Count.ToString();
            }

            case "occupant":
            {
                CountryState country = ParseCountry(arg1);
                return country.Units.Count == 0
                    ? "none"
                    : string.Join(",", country.Units.Keys.OrderBy(f => f.ToString()));
            }

            case "handsize": return DeckState.ForFaction(ParseFaction(arg1)).HandCardIds.Count.ToString();
            case "decksize": return DeckState.ForFaction(ParseFaction(arg1)).DeckCardIds.Count.ToString();

            // assert card <name|id> in hand|deck|discard|status|response
            case "card":
            {
                CardState card = ParseCard(arg1);
                DeckState deck = DeckState.ForFaction(card.Faction);
                if (deck.HandCardIds.Contains(card.Id))      return "hand";
                if (deck.DeckCardIds.Contains(card.Id))      return "deck";
                if (deck.DiscardedCardIds.Contains(card.Id)) return "discard";
                if (deck.StatusCardIds.Contains(card.Id))    return "status";
                if (deck.ResponseCardIds.Contains(card.Id))  return "response";
                return "nowhere";
            }

            default: return null;
        }
    }

    private static bool Compare(string actual, string op, string expected)
    {
        if (int.TryParse(actual, out int a) && int.TryParse(expected, out int b))
            return op switch
            {
                "==" => a == b, "!=" => a != b,
                ">" => a > b, ">=" => a >= b, "<" => a < b, "<=" => a <= b,
                _ => false,
            };

        // Non-numeric: only equality is meaningful, and it is case-insensitive so `assert step
        // play_card` works as well as PLAY_CARD.
        bool equal = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        return op switch { "==" => equal, "!=" => !equal, _ => false };
    }

    private static Faction ParseFaction(string value)
        => ParseEnum<Faction>(value, "faction");

    private static T ParseEnum<T>(string value, string what) where T : struct, Enum
        => value != null && Enum.TryParse(value, true, out T parsed)
            ? parsed : throw new Exception($"unknown {what} '{value}'");

    private static CountryState ParseCountry(string value)
        => (int.TryParse(value, out int id) ? CountryState.ForId(id) : CountryState.ForName(value))
           ?? throw new Exception($"unknown country '{value}'");

    private static CardState ParseCard(string value)
        => (int.TryParse(value, out int id) ? CardState.ForId(id) : CardState.ForName(value))
           ?? throw new Exception($"unknown card '{value}'");
}
