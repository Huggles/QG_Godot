using Godot;
using System;

public partial class CountryScene : Node2D
{
    public int CountryId { get; set; }
    private CountryState CountryState => CountryState.ForId(CountryId);

    public CountryData StaticCountryData => CountryState.StaticCountryData;
    public StraightState StraightState => CountryState.StraightState;

    public UnitScene UnitScene1;
    public UnitScene UnitScene2;
    public UnitScene UnitScene3;

    public Node2D UnitContainerNode => GetNode<Node2D>("UnitContainer");
    public Sprite2D SupplyStarSprite => GetNode<Sprite2D>("SupplyStarSprite");
    public Sprite2D StraightSpriteNode => GetNode<Sprite2D>("StraightSprite");
    public ClickableSprite CountrySprite => GetNode<ClickableSprite>("CountrySprite");

    public static readonly PackedScene CountryScenePacked = GD.Load<PackedScene>("res://scenes/World/Country.tscn");

    public static CountryScene SpawnCountry(int countryId)
    {
        CountryScene countrySceneInstance = CountryScenePacked.Instantiate<CountryScene>();                
        countrySceneInstance.CountryId = countryId;        
        countrySceneInstance.CountryState.Tags.TagAdded += countrySceneInstance.OnTagAdded;
        countrySceneInstance.CountryState.Tags.TagRemoved += countrySceneInstance.OnTagRemoved;
        NodeUtilities.Instance.CountriesNode.AddChild(countrySceneInstance, false);
        countrySceneInstance.Position = countrySceneInstance.StaticCountryData.WorldPositionCenter;
        return countrySceneInstance;
    }

    public override void _Ready()
    {
        Name = CountryState.Name; 
        if (StaticCountryData.Texture != null)
            ApplyTexture();

        //SetUnclickable();
        if (CountryState.IsSupply)
            ShowSupplyStar();
        else
            HideSupplyStar();

        SetUnclickable();
        
        StraightState.OnReady();

        CountrySprite.MouseLeftClickOnOpaque += OnMouseLeftClickOpaque;
    }

    private void OnTagAdded(Tag tag, Faction faction)
    {
        if (tag == Tag.Clickable)
        {
            SetClickable();
        }
    }

    private void OnTagRemoved(Tag tag, Faction faction)
    {
        if (tag == Tag.Clickable)
        {
            SetUnclickable();
        }
    }

    private void OnMouseLeftClickOpaque()
    {
        EventBus.Emit(EventBus.SignalName.CountryClicked, this.CountryState.Id);
    }


    private void ApplyTexture()
    {
        CountrySprite.SetTexture(StaticCountryData.Texture);
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

        unitScene.Position = new Vector2(transform.XPosition, transform.YPosition);        
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
        unitScene.Position = Vector2.Zero;

        switch (position)
        {
            case 1: UnitScene1 = null; break;
            case 2: UnitScene2 = null; break;
            case 3: UnitScene3 = null; break;
        }
    }

    public void SetClickable()
    {
        CountrySprite.ShowSprite();
        CountrySprite.SetClickable();
    }

    public void SetUnclickable()
    {
        CountrySprite.HideSprite();
        CountrySprite.SetUnclickable();
    }

    private void ShowSupplyStar()
    {
        SupplyStarSprite.Visible = true;

        if (StaticCountryData.SupplyStarTransformData != null)
        {
            var data = StaticCountryData.SupplyStarTransformData;
            SupplyStarSprite.Position = new Vector2(data.XPosition, data.YPosition);
            SupplyStarSprite.Scale = new Vector2(data.Scale, data.Scale);
        }
    }

    private void HideSupplyStar()
    {
        SupplyStarSprite.Visible = false;
    }
}
