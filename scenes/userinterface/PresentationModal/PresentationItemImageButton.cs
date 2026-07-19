using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class PresentationItemImageButton : PresentationItem
{
    public Texture2D Texture2D;
    public string ButtonText;
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
        this.ButtonText = null;
    }

    public PresentationItemImageButton(int identifier, string buttonText, bool selectable) : base(identifier, selectable)
    {
        this.Texture2D = null;
        this.ButtonText = buttonText;
    }

    public override Control InitializeControl()
    {        
        Control = PresentationItemImageButtonPackedPath.Instantiate<PresentationItemImageButtonControl>();
        _orderBadge = new Label();
        _orderBadge.Visible = false;
        _orderBadge.Position = new Vector2(8, 8);
        _orderBadge.ZIndex = 100;
        Control.AddChild(_orderBadge);
        return Control;
    }

    public override void LoadControl()
    {
        PresentationItemImageButtonControl.Size = ImageSize;
        PresentationItemImageButtonControl.CustomMinimumSize = ImageSize;
        PresentationItemImageButtonControl.ImageControl.Size = ImageSize;
        PresentationItemImageButtonControl.ImageControl.CustomMinimumSize = ImageSize;
        
        if (Texture2D != null)
        {
            PresentationItemImageButtonControl.ImageControl.Texture = Texture2D;
        }
        
        if (ButtonText != null)
        {
            PresentationItemImageButtonControl.Button.Text = ButtonText;
        }
        
        PresentationItemImageButtonControl.Button.Pressed += () => { EmitSignal(SignalName.ItemClicked, Identifier); };        
    }
}
