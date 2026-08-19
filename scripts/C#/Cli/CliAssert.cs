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

    /// <summary>
    /// `in` / `!in` are aliases for `==` / `!=`, so the pile assertions read naturally
    /// (`assert card 0 in deck`). They compare the same single value — a subject never yields a set.
    /// </summary>
    private static bool IsOperator(string token)
        => token is "==" or "!=" or ">" or ">=" or "<" or "<=" or "in" or "!in";

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

            // The open prompt, if any. `promptfaction` is NONE and `promptoptions` is -1 when
            // nothing is open, so both distinguish "not asked" from "asked with nothing on offer" —
            // a distinction the always-ask reaction rule made load-bearing. A faction holding a
            // face-down Response card is now asked in every reaction window whether or not it can
            // react, so "was a prompt raised" no longer tells you a card matched; only the option
            // count does. Tests that used to detect a wrongly-offered reaction by hanging on an
            // unexpected prompt must assert on the count instead.
            case "promptoptions":
                return (CliSession.Instance?.Input.OpenSpec?.Options.Count ?? -1).ToString();
            case "promptfaction":
                return (CliSession.Instance?.Input.OpenSpec?.Faction ?? Faction.NONE).ToString();
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

            // How many pieces of a type the faction still has to play: `assert pool GERMANY NAVY == 0`.
            // Not derivable from `units`, which reads the board — this reads what is NOT on it, and it
            // is the only way to see a faction has hit its QGData_Factions_V2.json cap.
            case "pool":
            {
                UnitType unitType = ParseEnum<UnitType>(subject.Count > 2 ? subject[2] : null, "unit type");
                return UnitPool.AvailableUnitCount(ParseFaction(arg1), unitType).ToString();
            }

            // Change events registered in the CURRENT round's pool — the surface every pool-scoped
            // Condition reads. `assert poolevents == 4` counts them all; `assert poolevents
            // RemoveUnitChangeEvent == 0` counts one kind, which is how a test proves an event carrying
            // RegisterInPool = false really stayed out of the pool rather than merely opening no window.
            case "poolevents":
            {
                List<ChangeEvent> pool = CardPlayPool.ChangeEventsPool;
                if (arg1 == null) return pool.Count.ToString();
                return pool.Count(changeEvent =>
                    changeEvent.GetType().Name.Equals(arg1, StringComparison.OrdinalIgnoreCase)).ToString();
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
            case "discardsize": return DeckState.ForFaction(ParseFaction(arg1)).DiscardedCardIds.Count.ToString();

            // Deck ORDER, which the state hash deliberately does not cover (it hashes only the deck
            // count). These are the only assertions that can catch a shuffle that silently did
            // nothing, or a client whose deck order drifted from the host's.
            case "decktop":
            {
                DeckState deck = DeckState.ForFaction(ParseFaction(arg1));
                return deck.DeckCardIds.Count == 0 ? "empty" : deck.DeckCardIds[0].ToString();
            }
            case "deckorder": return string.Join("-", DeckState.ForFaction(ParseFaction(arg1)).DeckCardIds);

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

            // assert cardrevealed <name|id> == true|false
            // Whether a Response card's face is public. Not the same question as "has it ever been
            // activated" (ActivatedInTurns): recycling a spent card back into play hides it again.
            case "cardrevealed": return ParseCard(arg1).IsRevealed ? "true" : "false";

            default: return null;
        }
    }

    private static bool Compare(string actual, string op, string expected)
    {
        if (op == "in")  op = "==";
        if (op == "!in") op = "!=";

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
