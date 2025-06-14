using Godot;
using System;

public partial class CountryScene : Node3D
{
    public CountryState CountryState { get; set; }

    public CountryData StaticCountryData => CountryState.StaticCountryData;
    public StraightState StraightState => CountryState.StraightState;

    public UnitScene UnitScene1;
    public UnitScene UnitScene2;
    public UnitScene UnitScene3;

    public Node3D UnitContainerNode => GetNode<Node3D>("UnitContainer");
    public ClickableSprite3D ClickableSpriteNode => GetNode<ClickableSprite3D>("ClickableSprite3D");
    public Sprite3D SupplyStarSprite => GetNode<Sprite3D>("SupplyStarSprite3D");
    public Sprite3D StraightSpriteNode => GetNode<Sprite3D>("StraightSprite3D");

    public bool clickable;

    public static readonly PackedScene CountryScenePacked = GD.Load<PackedScene>("res://scenes/World/Country.tscn");

    public static CountryScene SpawnCountry(CountryState countryState)
    {
        CountryScene countryScene = CountryScenePacked.Instantiate<CountryScene>();                
        countryScene.CountryState = countryState;
        countryScene.Name = countryState.Name;
        return countryScene;
    }

    public override void _Ready()
    {
        if (StaticCountryData.Texture != null)
            ApplyTexture();

        SetUnclickable();

        ClickableSpriteNode.Identifier = CountryState.Label;

        if (CountryState.IsSupply)
            ShowSupplyStar();
        else
            HideSupplyStar();

        StraightState.OnReady();
        SetDebugUnitPosition(false);
    }

    private void SetDebugUnitPosition(bool visible)
    {
        var pos1 = GetNode<Sprite3D>("DEBUG_UnitPositions/DEBUG_UnitPosition_Sprite1");
        pos1.Position = StaticCountryData.UnitTransformData.Position1.Position;
        pos1.Modulate = Colors.Green;
        pos1.Visible = visible;

        var pos2 = GetNode<Sprite3D>("DEBUG_UnitPositions/DEBUG_UnitPosition_Sprite2");
        pos2.Position = StaticCountryData.UnitTransformData.Position2.Position;
        pos2.Modulate = Colors.Blue;
        pos2.Visible = visible;

        var pos3 = GetNode<Sprite3D>("DEBUG_UnitPositions/DEBUG_UnitPosition_Sprite3");
        pos3.Position = StaticCountryData.UnitTransformData.Position3.Position;
        pos3.Modulate = Colors.Red;
        pos3.Visible = visible;
    }

    private void ApplyTexture()
    {
        ClickableSpriteNode.ClickableTexture = StaticCountryData.Texture;
    }

    public void AddUnit(UnitScene unitScene)
    {
        int position = SetUnitOnAvailablePosition(unitScene);

        if (position <= 0) return;

        unitScene.GetParent().RemoveChild(unitScene);
        UnitContainerNode.AddChild(unitScene);

        UnitTransformData transformData = StaticCountryData.UnitTransformData;
        if (transformData == null) return;

        var key = $"Position{position}";
        TransformData transform = (TransformData)transformData.GetValue(key);

        unitScene.Position = new Vector3(transform.XPosition, transform.YPosition, transform.ZPosition);
        unitScene.Scale = new Vector3(transform.Scale, transform.Scale, transform.Scale);
    }

    private int GetUnitPosition(UnitScene unitScene)
    {
        if (unitScene == UnitScene1) return 1;
        if (unitScene == UnitScene2) return 2;
        if (unitScene == UnitScene3) return 3;
        return -1;
    }

    private int SetUnitOnAvailablePosition(UnitScene unitScene)
    {
        if (UnitScene1 == null)
        {
            UnitScene1 = unitScene;
            return 1;
        }
        if (UnitScene2 == null)
        {
            UnitScene2 = unitScene;
            return 2;
        }
        if (UnitScene3 == null)
        {
            UnitScene3 = unitScene;
            return 3;
        }
        return -1;
    }

    public void RemoveUnit(UnitScene unitScene)
    {
        int position = GetUnitPosition(unitScene);
        if (position <= 0) return;

        UnitContainerNode.RemoveChild(unitScene);
        NodeUtilities.Instance.UnitsNode.AddChild(unitScene);
        unitScene.Position = Vector3.Zero;

        switch (position)
        {
            case 1: UnitScene1 = null; break;
            case 2: UnitScene2 = null; break;
            case 3: UnitScene3 = null; break;
        }
    }

    public void SetClickable()
    {
        clickable = true;
        ClickableSpriteNode.Enable();
    }

    public void SetUnclickable()
    {
        clickable = false;
        ClickableSpriteNode.Disable();
    }

    private void ShowSupplyStar()
    {
        SupplyStarSprite.Visible = true;

        if (StaticCountryData.SupplyStarTransformData != null)
        {
            var data = StaticCountryData.SupplyStarTransformData;
            SupplyStarSprite.Position = new Vector3(data.XPosition, data.YPosition, data.ZPosition);
            SupplyStarSprite.Scale = new Vector3(data.Scale, data.Scale, data.Scale);
        }
    }

    private void HideSupplyStar()
    {
        SupplyStarSprite.Visible = false;
    }

    // Connect this to the signal in the editor or via code
    private void _OnClickableSprite3DMouseEnterOpaque(ClickableSprite3D sprite)
    {
        // Optional: Add logic
    }

    // Connect this to the signal in the editor or via code
    private void _OnClickableSprite3DMouseLeftClickOpaque(ClickableSprite3D sprite)
    {
        GD.Print("_on_clickable_sprite_3d_mouse_left_click_opaque");

        if (clickable)
        {
            EventBus.Emit(EventBus.SignalName.CountryClicked, this.CountryState.Id);
        }
    }
}
