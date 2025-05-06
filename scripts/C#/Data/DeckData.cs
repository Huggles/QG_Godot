using Godot;
using System;
using System.Collections.Generic;

public partial class DeckData : DataObject
{
    public int FactionIndex { get; set; } = -1;    
    public List<DeckCardData> Cards  { get; set; }

    public Faction Faction {
        get { return (Faction)FactionIndex; }
    }
}
