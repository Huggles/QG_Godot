using Godot;
using System;
using System.Collections.Generic;

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

    public Faction Faction
    {
        get
        {
            return (Faction)Index;
        }
    }

    public Color FactionColor
    {
        get
        {
            return Color.FromString(ColorString, new Color(1, 1, 1, 1));
        }
    }
    public Color FactionColorText
    {
        get
        {
            return Color.FromString(ColorStringText, new Color(1, 1, 1, 1));
        }
    }

    public CountryState HomeSpaceCountryState
    {
        get
        {
            return CountryState.ForName(Homespace);
        }
    }

    public String FactionAdjactiveLabel
    {
        get
        {
            return new Dictionary<Faction, String>
            {
                { Faction.GERMANY, "German"},
                { Faction.UNITED_KINGDOM, "United Kingdom"},
                { Faction.JAPAN, "Japanese"},
                { Faction.SOVIET, "Soviet"},
                { Faction.ITALY, "Italian"},
                { Faction.UNITED_STATES, "United States"}
            }[Faction];
        }
    }
    public class CountryLabels
    {
        public string Label;
        public string
    }
}