using Godot;
using System;

public enum Faction
{
    NONE, GERMANY, UNITED_KINGDOM, JAPAN, SOVIET, ITALY, UNITED_STATES, ALL,

    // Minor factions an expansion brings to the board; see StaticGameData.MinorFactions. Appended after
    // ALL so every value above keeps the number saves, the wire and TagContainer's bit masks already use.
    // They only exist in a session whose expansions load their faction data.
    FRANCE, CHINA
}
