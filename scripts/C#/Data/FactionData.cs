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

    public Faction Faction => (Faction)Index;

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

    [JsonIgnore] public Texture2D FlagTexture => FactionFlags[Faction];

    public string FactionAdjactiveLabel => FactionAdjactiveLabels[Faction];

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