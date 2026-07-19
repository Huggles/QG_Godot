using Godot;

/// <summary>
/// HUD button that toggles the auto-dismiss-modal setting on/off.
/// </summary>
public partial class AutoDismissControl : PanelContainer
{
    private Button _toggleButton;

    public override void _Ready()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        AddChild(margin);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(hbox);

        var label = new Label();
        label.Text = "Auto-dismiss:";
        hbox.AddChild(label);

        _toggleButton = new Button();
        _toggleButton.Pressed += OnTogglePressed;
        hbox.AddChild(_toggleButton);

        UpdateButton();
    }

    private void OnTogglePressed()
    {
        GameSettings.Instance.SetAutoDismissModal(!GameSettings.Instance.AutoDismissModal);
        UpdateButton();
    }

    private void UpdateButton()
    {
        _toggleButton.Text = GameSettings.Instance.AutoDismissModal ? "On" : "Off";
    }
}
