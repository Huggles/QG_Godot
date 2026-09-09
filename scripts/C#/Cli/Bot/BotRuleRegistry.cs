using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Every rule that exists, in a fixed order, plus the parser for the <c>bot_rules</c> argument.
///
/// The order of <see cref="All"/> is the consult order and is therefore part of the run's behaviour
/// (see the float-associativity note on <see cref="BotRuleEngine"/>). Add new rules at the END unless
/// you intend to change existing results.
///
/// Two deliberate strictnesses, both because a configured policy is only useful if you can trust that
/// what you typed is what ran:
///
///  1. A Kinds value that does not name a real request type is a construction failure, not a rule that
///     silently never fires.
///  2. An unknown rule name in <c>bot_rules</c> is a hard error that stops the run before the game
///     starts. The alternative is a batch of two thousand games that quietly measured the default
///     policy and answered the wrong question — which costs an afternoon and, worse, produces a
///     confident wrong number.
/// </summary>
public static class BotRuleRegistry
{
    /// <summary>
    /// The rules, in consult order. One line per rule; the rule itself lives in Rules/.
    /// </summary>
    public static List<IBotRule> All() => new()
    {
        new NoHollowRule(),
        new AvoidEmptyBattleRule(),
        new AvoidDeadBattleCardRule(),
    };

    /// <summary>
    /// The <see cref="InputRequestSpec.Kind"/> strings that actually exist, derived from the request
    /// types themselves rather than written down twice. InputRequestSpec builds Kind as the nested type
    /// name minus the "RequestHandler" suffix, so this mirrors that one transformation.
    /// </summary>
    public static HashSet<string> ValidKinds() => typeof(InputRequest)
        .GetNestedTypes()
        .Where(t => t.Name.EndsWith("RequestHandler", StringComparison.Ordinal))
        .Select(t => t.Name.Replace("RequestHandler", ""))
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Resolve <c>bot_rules</c> into one config per registered rule.
    ///
    /// Syntax, in the project's existing comma-list convention (see CliArgs' note on
    /// <c>inject_error=a,b</c>):
    ///
    ///   bot_rules=no_hollow,prefer_x:2.0       enable, the second with weight 2
    ///   bot_rules=no_hollow:suppress=0.3       enable, skipped on 30% of prompts
    ///   bot_rules=-no_hollow                   disable a rule that is on by default
    ///   bot_rules=all                          every rule at its default weight
    ///   bot_rules=none                         no rules; the bot is uniform random again
    ///
    /// Naming any rule positively switches OFF every default-on rule that is not named, so that a
    /// command line reads as the whole policy rather than as a delta against defaults nobody
    /// remembers. Use the <c>-name</c> form to keep the defaults and drop one.
    ///
    /// Every number is parsed with InvariantCulture, which is load-bearing twice over here: a
    /// comma-decimal machine would otherwise both misread <c>2.0</c> and split a locale-formatted
    /// <c>2,0</c> into two garbage entries.
    /// </summary>
    /// <param name="spec">The raw argument, or null/empty for "just the defaults".</param>
    /// <param name="rules">The registered rules, from <see cref="All"/>.</param>
    /// <param name="error">Set to a human-readable reason when this returns null.</param>
    public static Dictionary<string, BotRuleConfig> Parse(string spec, List<IBotRule> rules,
                                                          out string error)
    {
        error = null;

        Dictionary<string, BotRuleConfig> config = rules.ToDictionary(
            r => r.Name,
            r => new BotRuleConfig { Enabled = r.EnabledByDefault, Weight = r.DefaultWeight, Suppress = 0 });

        if (string.IsNullOrWhiteSpace(spec)) return config;

        string trimmed = spec.Trim();
        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            foreach (BotRuleConfig entry in config.Values) entry.Enabled = false;
            return config;
        }
        if (trimmed.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            foreach (BotRuleConfig entry in config.Values) entry.Enabled = true;
            return config;
        }

        List<string> entries = trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        // An explicit positive list replaces the defaults; a list of only -disables refines them.
        bool anyPositive = entries.Any(e => !e.StartsWith("-", StringComparison.Ordinal));
        if (anyPositive)
            foreach (BotRuleConfig entry in config.Values) entry.Enabled = false;

        foreach (string entry in entries)
        {
            bool disable = entry.StartsWith("-", StringComparison.Ordinal);
            string[] parts = (disable ? entry[1..] : entry).Split(':', StringSplitOptions.TrimEntries);
            string name = parts[0];

            if (name.Length == 0)
            {
                error = $"empty rule name in bot_rules entry \"{entry}\"";
                return null;
            }

            // Catches a locale-formatted weight that got split on its own decimal comma, which would
            // otherwise be reported as the far more baffling "unknown rule: 0".
            if (double.TryParse(name, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                error = $"\"{name}\" is a number, not a rule name — write a weight as name:{name}, " +
                        $"and use a decimal POINT (bot_rules is comma-separated)";
                return null;
            }

            if (!config.TryGetValue(name, out BotRuleConfig target))
            {
                error = $"unknown bot rule \"{name}\". Known rules: {string.Join(", ", config.Keys.OrderBy(k => k, StringComparer.Ordinal))}";
                return null;
            }

            if (disable)
            {
                target.Enabled = false;
                if (parts.Length > 1)
                {
                    error = $"\"-{name}\" disables a rule, so it takes no options";
                    return null;
                }
                continue;
            }

            target.Enabled = true;

            foreach (string option in parts.Skip(1))
            {
                if (option.Length == 0) continue;

                // A bare number is sugar for the weight, which is the option anyone actually tunes.
                if (double.TryParse(option, NumberStyles.Float, CultureInfo.InvariantCulture, out double bare))
                {
                    target.Weight = bare;
                    continue;
                }

                string[] pair = option.Split('=', 2, StringSplitOptions.TrimEntries);
                if (pair.Length != 2)
                {
                    error = $"rule \"{name}\": expected key=value or a bare weight, got \"{option}\"";
                    return null;
                }

                if (!double.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    error = $"rule \"{name}\": \"{pair[1]}\" is not a number (use a decimal point)";
                    return null;
                }

                switch (pair[0].ToLowerInvariant())
                {
                    // Unclamped on purpose: a weight is a scale, not a probability.
                    case "w":
                    case "weight":
                        target.Weight = value;
                        break;
                    case "suppress":
                        target.Suppress = Math.Clamp(value, 0, 1);
                        break;
                    default:
                        error = $"rule \"{name}\": unknown option \"{pair[0]}\" (known: w, suppress)";
                        return null;
                }
            }
        }

        return config;
    }

    /// <summary>
    /// Validate that every rule's declared Kinds name a real request type. Returns null when fine.
    ///
    /// A typo here is the failure this whole registry is built to prevent: the rule loads, reports
    /// itself enabled, is never consulted because no prompt ever matches, and its statistics row reads
    /// all zeroes — which is indistinguishable from a rule that simply had nothing to do.
    /// </summary>
    public static string ValidateKinds(List<IBotRule> rules)
    {
        HashSet<string> valid = ValidKinds();

        foreach (IBotRule rule in rules)
        {
            if (rule.Kinds == null) continue;
            foreach (string kind in rule.Kinds)
                if (!valid.Contains(kind))
                    return $"bot rule \"{rule.Name}\" declares unknown prompt kind \"{kind}\". " +
                           $"Known kinds: {string.Join(", ", valid.OrderBy(k => k, StringComparer.Ordinal))}";
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (IBotRule rule in rules)
            if (!seen.Add(rule.Name))
                return $"duplicate bot rule name \"{rule.Name}\"";

        return null;
    }
}
