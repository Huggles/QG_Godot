using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

public abstract partial class PresentationItem : GodotObject
{
    public int Identifier;
    public bool Selectable;
    public Control Control;

    public abstract Control InitializeControl();
    public virtual void LoadControl() { }

    [Signal] public delegate void ItemClickedEventHandler(int identifier);

    public PresentationItem(int identifier, bool selectable)
    {
        this.Identifier = identifier;
        this.Selectable = selectable;
    }

    public static List<PresentationItem> ForFactions(List<Faction> factions)
    {
        List<PresentationItem> presentationItems = factions.Map((faction) =>
        {
            return (PresentationItem)new PresentationItemImageButton((int)faction, FactionData.FactionFlags[faction], true);
        });
        return presentationItems;
    }
}
