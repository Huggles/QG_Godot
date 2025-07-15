using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract partial class PresentationItem : GodotObject
{
    public int Identifier;
    public bool Selectable;
    public Control Control;

    public abstract Control InitializeControl();
    public virtual void LoadControl(){}

    [Signal] public delegate void ItemClickedEventHandler(int identifier);

    public PresentationItem(int identifier, bool selectable)
    {
        this.Identifier = identifier;
        this.Selectable = selectable;
    }
}
