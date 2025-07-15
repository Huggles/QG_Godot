using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class PresentationItemImageButton : PresentationItem
{
    public Texture2D Texture2D;
    private Vector2 ImageSize = new Vector2(300, 150);

    public PresentationItemImageButtonControl PresentationItemImageButtonControl {
        get {
            return Control as PresentationItemImageButtonControl;
        }
    }

    public static readonly PackedScene PresentationItemImageButtonPackedPath = GD.Load<PackedScene>("res://scenes/userinterface/PresentationModal/PresentationItemImageButton.tscn");

    public PresentationItemImageButton(int identifier, Texture2D texture2D, bool selectable) : base(identifier, selectable)
    {
        this.Texture2D = texture2D;
    }

    public override Control InitializeControl()
    {        
        Control = PresentationItemImageButtonPackedPath.Instantiate<PresentationItemImageButtonControl>();
        return Control;
    }

    public override void LoadControl()
    {
        PresentationItemImageButtonControl.Size = ImageSize;
        PresentationItemImageButtonControl.CustomMinimumSize = ImageSize;
        PresentationItemImageButtonControl.ImageControl.Size = ImageSize;
        PresentationItemImageButtonControl.ImageControl.CustomMinimumSize = ImageSize;
        PresentationItemImageButtonControl.ImageControl.Texture = Texture2D;
        PresentationItemImageButtonControl.Button.Pressed += () => { EmitSignal(SignalName.ItemClicked, Identifier); };        
    }
}
