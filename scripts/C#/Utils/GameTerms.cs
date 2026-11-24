using System.Collections.Generic;
using Godot;

/// <summary>
/// The game's fixed names and terms (factions, countries, units, card types, turn steps) in the
/// player's language. See <c>docs/LOCALIZATION_GLOSSARY.md</c> for the agreed wording.
///
/// Display only. The English names (<see cref="FactionDisplay.Label"/>, <c>CountryData.Label</c>,
/// <see cref="CardTypeDisplay.Label"/>) stay what the CLI, AI tooling, tutorial scripts and network
/// messages match on and send, so never feed a value from here back into game logic.
///
/// Every method falls back to the English when the key is missing, so a term added to the data before
/// it reaches the .po files still shows.
///
/// Faction + unit combinations ("German Army", "Duits leger") are one key each, not an adjective glued
/// to a noun: Dutch adjectives change form with the noun ("Duits leger" but "Duitse vloot"), and other
/// languages put the words in a different order.
/// </summary>
public static class GameTerms
{
    /// <summary>"Germany" / "Duitsland". Key <c>FACTION_&lt;FACTION&gt;</c>.</summary>
    public static string FactionName(Faction faction)
        => Localization.TranslateOr($"FACTION_{faction}", faction.Label());

    /// <summary>"German Army" / "Duits leger". Key <c>FACTION_&lt;FACTION&gt;_ARMY</c>.</summary>
    public static string FactionArmyLabel(Faction faction) => FactionNoun(faction, "ARMY", "Army");

    /// <summary>"German Navy" / "Duitse vloot". Key <c>FACTION_&lt;FACTION&gt;_NAVY</c>.</summary>
    public static string FactionNavyLabel(Faction faction) => FactionNoun(faction, "NAVY", "Navy");

    /// <summary>"German Air Force" / "Duitse luchtmacht". Key <c>FACTION_&lt;FACTION&gt;_AIR_FORCE</c>.</summary>
    public static string FactionAirForceLabel(Faction faction) => FactionNoun(faction, "AIR_FORCE", "Air Force");

    /// <summary>The faction's piece of <paramref name="type"/>: Army, Navy or Air Force.</summary>
    public static string FactionUnitLabel(Faction faction, UnitType type) => type switch
    {
        UnitType.NAVY      => FactionNavyLabel(faction),
        UnitType.AIR_FORCE => FactionAirForceLabel(faction),
        _                  => FactionArmyLabel(faction),
    };

    /// <summary>"German Discard Pile" / "Duitse aflegstapel".</summary>
    public static string FactionDiscardPileLabel(Faction faction) => FactionNoun(faction, "DISCARD_PILE", "Discard Pile");

    /// <summary>"German Status Cards" / "Duitse Statuskaarten".</summary>
    public static string FactionStatusCardsLabel(Faction faction) => FactionNoun(faction, "STATUS_CARDS", "Status Cards");

    /// <summary>"German Response Cards" / "Duitse Reactiekaarten".</summary>
    public static string FactionResponseCardsLabel(Faction faction) => FactionNoun(faction, "RESPONSE_CARDS", "Response Cards");

    /// <summary>"Axis" / "As". Key <c>TEAM_&lt;TEAM&gt;</c>.</summary>
    public static string TeamName(FactionTeam team)
        => Localization.TranslateOr($"TEAM_{team}", team.ToString());

    /// <summary>"Ukraine" / "Oekraïne", by the country's UniqueName. Key <c>COUNTRY_&lt;UNIQUENAME&gt;</c>.</summary>
    public static string CountryName(CountryData country)
        => country == null ? "" : Localization.TranslateOr($"COUNTRY_{country.UniqueName}", country.Label);

    /// <summary>"Army" / "Leger", or the plural. Key <c>UNIT_&lt;TYPE&gt;</c> / <c>UNIT_&lt;TYPE&gt;_PLURAL</c>.</summary>
    public static string UnitName(UnitType type, bool plural = false)
    {
        (string singular, string many) = type switch
        {
            UnitType.NAVY      => ("Navy", "Navies"),
            UnitType.AIR_FORCE => ("Air Force", "Air Forces"),
            _                  => ("Army", "Armies"),
        };
        string key = $"UNIT_{(type == UnitType.ANY ? UnitType.ARMY : type)}{(plural ? "_PLURAL" : "")}";
        return Localization.TranslateOr(key, plural ? many : singular);
    }

    /// <summary>"Land Battle" / "Landslag". Key <c>CARDTYPE_&lt;TYPE&gt;</c>.</summary>
    public static string CardTypeName(CardType type)
        => Localization.TranslateOr($"CARDTYPE_{type}", type.Label());

    /// <summary>"Supply" / "Bevoorrading". Key <c>TURNSTEP_&lt;STEP&gt;_NAME</c>.</summary>
    public static string TurnStepName(TurnStep step)
        => Localization.TranslateOr($"TURNSTEP_{step}_NAME", TurnStepText.For(step).Name);

    /// <summary>The step's one-line rule. Key <c>TURNSTEP_&lt;STEP&gt;_RULE</c>.</summary>
    public static string TurnStepRule(TurnStep step)
        => Localization.TranslateOr($"TURNSTEP_{step}_RULE", TurnStepText.For(step).Rule);

    private static string FactionNoun(Faction faction, string noun, string englishNoun)
        => Localization.TranslateOr($"FACTION_{faction}_{noun}", $"{EnglishAdjective(faction)} {englishNoun}");

    /// <summary>The English fallback adjective, as the game has always printed it ("United Kingdom Army").</summary>
    private static string EnglishAdjective(Faction faction)
    {
        FactionData data = StaticGameData.FactionDataMap.GetValueOrDefault(faction);
        return data?.FactionAdjactiveLabel ?? faction.Label();
    }
}
