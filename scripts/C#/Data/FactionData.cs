using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public partial class FactionData : DataObject
{
    public int Index { get; set; }
    public string UniqueName { get; set; }
    public string Label { get; set; }
    public string ColorString { get; set; }
    public string ColorStringText { get; set; }
    public string Team { get; set; }
    public string Homespace { get; set; }
    public int NumberOfArmyUnits { get; set; }
    public int NumberOfNavyUnits { get; set; }

    /// <summary>Air Forces in the faction's pool. 0 unless an expansion gives it some.</summary>
    public int NumberOfAirUnits { get; set; }

    /// <summary>
    /// Set only on a minor faction: the playable faction that acts for it (checks its supply, scores it,
    /// answers its prompts). A minor faction has pieces on the board but no seat, no deck and no turn.
    /// </summary>
    public string PatronFaction { get; set; }

    /// <summary>Optional. "French", for rules text; the six base factions use FactionAdjactiveLabels.</summary>
    public string AdjectiveLabel { get; set; }

    /// <summary>Optional res:// path of the flag; the six base factions use FactionFlags.</summary>
    public string FlagPath { get; set; }

    public Faction Faction => (Faction)Index;

    [JsonIgnore] public Faction Patron => Enum.TryParse(PatronFaction, out Faction patron) ? patron : Faction.NONE;

    [JsonIgnore] public bool IsMinor => Patron != Faction.NONE;

    [JsonIgnore] public Color FactionColor => Color.FromString(ColorString, new Color(1, 1, 1, 1));

    [JsonIgnore] public Color FactionColorText => Color.FromString(ColorStringText, new Color(1, 1, 1, 1));

    public string factionName => Faction.ToString().ToLower();
    public string factionNameCapitalized => factionName.Capitalize().Replace(" ", "_");

    [JsonIgnore] public CountryState HomeSpaceCountryState => CountryState.ForName(Homespace);

    [JsonIgnore] public Texture2D CardBackTexture
    {
        get
        {
            if(field == null)
            {
                field = GD.Load<Texture2D>(String.Format(CardTexturePath, factionName, factionNameCapitalized, "CardBack"));
            }
            return field;
        }
    }

    [JsonIgnore] public Texture2D FlagTexture
        => FlagPath == null ? FactionFlags[Faction] : _flagTexture ??= GD.Load<Texture2D>(FlagPath);
    private Texture2D _flagTexture;

    /// <summary>The flag as a full card front, for a faction drawn as a card; null for one without (the minor factions).</summary>
    [JsonIgnore] public Texture2D FlagCardTexture
        => FlagCardPaths.TryGetValue(Faction, out string path) ? _flagCardTexture ??= GD.Load<Texture2D>(path) : null;
    private Texture2D _flagCardTexture;

    private static readonly Dictionary<Faction, string> FlagCardPaths = new()
    {
        { Faction.GERMANY,        "res://assets/factions/germany/Germany_FlagCard.png" },
        { Faction.UNITED_KINGDOM, "res://assets/factions/united_kingdom/UK_FlagCard.png" },
        { Faction.JAPAN,          "res://assets/factions/japan/Japan_FlagCard.png" },
        { Faction.SOVIET,         "res://assets/factions/soviet/Soviet_FlagCard.png" },
        { Faction.ITALY,          "res://assets/factions/italy/Italy_FlagCard.png" },
        { Faction.UNITED_STATES,  "res://assets/factions/united_states/US_FlagCard.png" },
    };

    public string FactionAdjactiveLabel => AdjectiveLabel ?? FactionAdjactiveLabels[Faction];

    public static Dictionary<Faction, string> FactionAdjactiveLabels => new Dictionary<Faction, string>
            {
                { Faction.GERMANY, "German"},
                { Faction.UNITED_KINGDOM, "United Kingdom"},
                { Faction.JAPAN, "Japanese"},
                { Faction.SOVIET, "Soviet"},
                { Faction.ITALY, "Italian"},
                { Faction.UNITED_STATES, "United States"}
            };


    [JsonIgnore] public Dictionary<CardType, Texture2D> CardFrontTextures = new Dictionary<CardType, Texture2D>();
    private Dictionary<Faction, List<CardType>> excludeCards = new Dictionary<Faction, List<CardType>>
    {
        { Faction.GERMANY, [CardType.RESPONSE]},
        { Faction.UNITED_KINGDOM, []},
        { Faction.JAPAN, [CardType.EVENT]},
        { Faction.SOVIET, [CardType.ECONOMIC_WARFARE]},
        { Faction.ITALY, []},
        { Faction.UNITED_STATES, [CardType.RESPONSE]}
    };
    private string CardTexturePath = "res://assets/factions/{0}/cards/{1}_{2}.png";

    public static readonly Texture2D GERMANY_FLAG_TEXTURE =         GD.Load<Texture2D>("res://assets/factions/germany/Germany_Flag.png");
    public static readonly Texture2D UNITED_KINGDOM_FLAG_TEXTURE =  GD.Load<Texture2D>("res://assets/factions/united_kingdom/UK_Flag.png");
    public static readonly Texture2D JAPAN_FLAG_TEXTURE =           GD.Load<Texture2D>("res://assets/factions/japan/Japan_Flag.png");
    public static readonly Texture2D SOVIET_FLAG_TEXTURE =          GD.Load<Texture2D>("res://assets/factions/soviet/Soviet_Flag.png");
    public static readonly Texture2D ITALY_FLAG_TEXTURE =           GD.Load<Texture2D>("res://assets/factions/italy/Italy_Flag.png");
    public static readonly Texture2D UNITED_STATES_FLAG_TEXTURE =   GD.Load<Texture2D>("res://assets/factions/united_states/US_Flag.png");
    public static Dictionary<Faction, Texture2D> FactionFlags = new Dictionary<Faction, Texture2D>
    {
            { Faction.GERMANY,         GERMANY_FLAG_TEXTURE },
            { Faction.UNITED_KINGDOM,  UNITED_KINGDOM_FLAG_TEXTURE },
            { Faction.JAPAN,           JAPAN_FLAG_TEXTURE },
            { Faction.SOVIET,          SOVIET_FLAG_TEXTURE },
            { Faction.ITALY,           ITALY_FLAG_TEXTURE },
            { Faction.UNITED_STATES,   UNITED_STATES_FLAG_TEXTURE }
    };
    
    public void LoadTextures()
    {
        // A headless/dedicated server has no rendering and does not import textures — skip entirely.
        if (GameContext.IsHeadless) return;
        // A minor faction has no deck, so no card art either.
        if (IsMinor) return;
        LoadCardTextures();
    }
    private void LoadCardTextures()
    {
        
        foreach (CardType cardType in Enum.GetValues(typeof(CardType)))
        {
            if (excludeCards[Faction].Contains(cardType))
            {
                continue;
            }
            string texturePath;
            if (cardType == CardType.NONE)
            {
                texturePath = String.Format(CardTexturePath, factionName, factionNameCapitalized, "CardBack");
            }
            else if (!cardType.IsPlayedFromHand())
            {
                // Expansion card types: their art ships with the expansion, and a session without it has none of these cards.
                texturePath = ContentPaths.Resolve($"factions/{factionName}/cards/{factionNameCapitalized}_{cardType.ToString().Capitalize().Replace(" ", "")}.png");
                if (!ResourceLoader.Exists(texturePath)) continue;
            }
            else
            {
                texturePath = String.Format(CardTexturePath, factionName, factionNameCapitalized, cardType.ToString().Capitalize().Replace(" ", ""));
            }
            DebugUtilities.PrintPeerFinest(texturePath);
            Texture2D texture2D = GD.Load<Texture2D>(texturePath);
            if (texture2D == null)
            {
                throw new Exception("Texture could not be loaded: " + texturePath);
            }
            CardFrontTextures.Add(cardType, texture2D);
        }
    }
    
    




    
    
}