using Godot;
using System;
using System.Collections.Generic;

public partial class DeckCardData : DataObject
{
    public string CardName { get; set; }
    public int Number { get; set; } = 1;
}
