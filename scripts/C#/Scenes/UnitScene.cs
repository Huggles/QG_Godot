using Godot;
using System;

public partial class UnitScene : Node2D
{
    // Constants and static references
    private const string ArmySpritePath = "res://assets/textures/Units/QGArmy.png";
    private const string NavySpritePath = "res://assets/textures/Units/QGNavy.png";

    private static readonly Texture2D ArmySprite = GD.Load<Texture2D>(ArmySpritePath);
    private static readonly Texture2D NavySprite = GD.Load<Texture2D>(NavySpritePath);
    private static readonly PackedScene UnitScenePacked = GD.Load<PackedScene>("res://scenes/Units/Unit.tscn");

    // State
    [Export]
    public UnitState UnitState { get; set; }

    // Node Accessors
    private Sprite2D UnitSpriteNode => GetNode<Sprite2D>("UnitSprite2D");
    private ClickableSprite TargetSprite => GetNode<ClickableSprite>("TargetSprite");
    private Sprite2D OutOfSupplyNode => GetNode<Sprite2D>("OutOfSupplyIcon");

    // Clickable
    private bool clickable;

    // Materials
    private ShaderMaterial normalShaderMaterial = GD.Load<ShaderMaterial>("res://assets/materials/unit_shader_material.tres").Duplicate() as ShaderMaterial;
    private ShaderMaterial outOfSupplyShaderMaterial = GD.Load<ShaderMaterial>("res://assets/materials/UnitOutOfSupplyShaderMaterial.tres").Duplicate() as ShaderMaterial;

    // Signals
    [Signal]
    public delegate void UnitClickedEventHandler();

    [Signal]
    public delegate void UnitDoubleClickedEventHandler();

    // Properties
    private bool IsArmy => UnitType == UnitType.ARMY;
    private bool IsNavy => UnitType == UnitType.NAVY;

    private UnitType UnitType => UnitState.Type;
    private Faction Faction => UnitState.Faction;
    private FactionData FactionData => StaticGameData.FactionDataMap[Faction];
    private CountryState CountryState => UnitState.CountryState;

    // Factory
    public static UnitScene SpawnUnit(UnitState unitState)
    {
        UnitScene unitSceneInstance = UnitScenePacked.Instantiate<UnitScene>();
        unitSceneInstance.UnitState = unitState;

        var factionData = StaticGameData.FactionDataMap[unitState.Faction];
        unitSceneInstance.Name = $"{factionData.UniqueName}_{unitState.Type}";
        
        unitState.BeforeUnitDeployedToCountry += unitSceneInstance.OnBeforeUnitDeployedToCountry;
        unitState.BeforeUnitRemovedFromCountry += unitSceneInstance.OnBeforeUnitRemovedFromCountry;

        unitState.AfterUnitDeployedToCountry += unitSceneInstance.OnAfterUnitDeployedToCountry;
        unitState.AfterUnitRemovedFromCountry += unitSceneInstance.OnAfterUnitRemovedFromCountry;

        return unitSceneInstance;
    }

    public override void _Ready()
    {
        SetSprite();
        SetUnclickable();
        if (UnitState.CountryId >= 0 && !UnitState.InSupply)
        {
            ShowOutOfSupply();
        }
        else
        {
            HideOutOfSupply();
        }
        
        TargetSprite.MouseLeftClickOnOpaque += OnMouseLeftClickOpaque;
    }
    private void OnMouseLeftClickOpaque()
    {
        EventBus.Emit(EventBus.SignalName.UnitClicked, this.UnitState.Id);
    }


    private void SetSprite()
    {
        if (IsArmy)
        {
            UnitSpriteNode.Texture = ArmySprite;
        }
        else if (IsNavy)
        {
            UnitSpriteNode.Texture = NavySprite;
        }

        UnitSpriteNode.Modulate = FactionData.FactionColor;
    }

    public void SetClickable()
    {
        TargetSprite.ShowSprite();
        TargetSprite.SetClickable();
    }

    public void SetUnclickable()
    {        
        TargetSprite.HideSprite();
        TargetSprite.SetUnclickable();
    }

    public void ShowOutOfSupply()
    {
        OutOfSupplyNode.Visible = true;
    }

    public void HideOutOfSupply()
    {
        OutOfSupplyNode.Visible = false;
    }

    

    private void OnBeforeUnitDeployedToCountry()
    {
        // Hook, no action needed
    }

    private async void OnAfterUnitDeployedToCountry()
    {
        CountryState.Node.AddUnit(this);

        Tween tween = CreateTween();
        tween.TweenProperty(UnitSpriteNode, "scale", new Vector2(1.5f, 1.5f), 0.2);
        tween.TweenProperty(UnitSpriteNode, "scale", new Vector2(1, 1), 0.2);
        await ToSignal(tween, "finished");
    }

    private async void OnBeforeUnitRemovedFromCountry(int unitId, int countryId)
    {
        Tween tween = CreateTween(); 
        tween.TweenProperty(UnitSpriteNode, "scale", new Vector2(1.2f, 1.2f), 0.2);
        tween.TweenProperty(UnitSpriteNode, "scale", new Vector2(0,0), 0.2);
        await ToSignal(tween, "finished");

        CountryState.ForId(countryId).Node.RemoveUnit(this);
    }

    private void OnAfterUnitRemovedFromCountry()
    {
        // Hook, no action needed
    }

    private void OnClickableSprite3DMouseLeftClickOpaque(ClickableSprite3D sprite)
    {
        if (clickable)
        {
            EventBus.Emit(EventBus.SignalName.UnitClicked, this.UnitState.Id);
        }
    }
}
