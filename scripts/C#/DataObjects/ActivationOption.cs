using Godot;
using System;

public partial class ActivationOption : GodotObject
{    
    public static int IdentifierCounter = 100000;
    
    public int Identifier { get; set; }
    public string Label { get; set; }

    public ActivationOption(int identifier, string label)
    {
        if (identifier > -1)
        {
            this.Identifier = identifier;
        }
        else
        {
            this.Identifier = IdentifierCounter;
            IdentifierCounter += 1;
        }
        
        this.Label = label;
    }
}
