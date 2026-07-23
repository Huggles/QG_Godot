using Godot;

/// <summary>
/// A <see cref="PresentationItem"/> that renders a text-label button using the standard
/// styled <see cref="MenuPanelButton"/> look. Parallels <see cref="PresentationItemImageButton"/>
/// (image variant) for the text-option case, e.g. the "Choose an action" modal.
/// </summary>
public partial class PresentationItemTextButton : PresentationItem
{
    public string ButtonText;
    private static readonly Vector2 ButtonSize = new Vector2(400, 150);

    public static readonly PackedScene MenuPanelButtonPackedScene =
        GD.Load<PackedScene>("res://scenes/menu/MenuPanelButton.tscn");

    private MenuPanelButton MenuPanelButton => Control as MenuPanelButton;

    public PresentationItemTextButton(int identifier, string buttonText, bool selectable) : base(identifier, selectable)
    {
        this.ButtonText = buttonText;
    }

    public override Control InitializeControl()
    {
        Control = MenuPanelButtonPackedScene.Instantiate<MenuPanelButton>();
        Control.CustomMinimumSize = ButtonSize;

        _orderBadge = new Label();
        _orderBadge.Visible = false;
        _orderBadge.Position = new Vector2(8, 8);
        _orderBadge.ZIndex = 100;
        _orderBadge.Scale = new Vector2(0.5f, 0.5f);
        Control.AddChild(_orderBadge);

        return Control;
    }

    public override void LoadControl()
    {
        MenuPanelButton.ButtonText = ButtonText;
        MenuPanelButton.Pressed += () => { EmitSignal(SignalName.ItemClicked, Identifier); };
    }
}
