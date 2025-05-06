using Godot;
using System;

public partial class UnitScene : Node3D
{
    // Constants and static references
    private const string ArmySpritePath = "res://assets/textures/Units/QGArmy.png";
    private const string NavySpritePath = "res://assets/textures/Units/QGNavy.png";

    private static readonly Texture2D ArmySprite = GD.Load<Texture2D>(ArmySpritePath);
    private static readonly Texture2D NavySprite = GD.Load<Texture2D>(NavySpritePath);
    private static readonly PackedScene UnitScenePacked = GD.Load<PackedScene>("res://scenes/Units/Unit.tscn");

    // State
    public UnitState UnitState { get; set; }

    // Node Accessors
    private Sprite3D UnitSpriteNode => GetNode<Sprite3D>("UnitSprite3D");
    private ClickableSprite3D ClickableSpriteNode => GetNode<ClickableSprite3D>("ClickableSprite3D");
    private Sprite3D OutOfSupplyNode => GetNode<Sprite3D>("OutOfSupplyIcon");
    private Label3D DebugLabelNode => GetNode<Label3D>("DebugLabel3D");

    // Clickable
    private bool clickable;
    private Callable clickCallback;

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
        DebugLabelNode.Visible = false;
        ClickableSpriteNode.Identifier = $"{UnitState.Faction}{UnitState.Id}";

        if (UnitState.CountryId >= 0 && !UnitState.InSupply)
            ShowOutOfSupply();
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
        UnitSpriteNode.SortingOffset = 50 + (int)FactionData.Faction;
    }

    public void SetClickable(Callable callback)
    {
        clickable = true;
        clickCallback = callback;
        HideOutOfSupply();
        ClickableSpriteNode.Enable();
    }

    public void SetUnclickable()
    {
        clickable = false;
        ShowOutOfSupply();
        ClickableSpriteNode.Disable();
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

        var tween = GetTree().CreateTween();
        tween.TweenProperty(UnitSpriteNode, "pixel_size", 0.04, 0.2);
        tween.TweenProperty(UnitSpriteNode, "pixel_size", 0.02, 0.2);
        await ToSignal(tween, "finished");
    }

    private async void OnBeforeUnitRemovedFromCountry(int unitId, int countryId)
    {
        var tween = GetTree().CreateTween();
        tween.TweenProperty(UnitSpriteNode, "pixel_size", 0.025, 0.2);
        tween.TweenProperty(UnitSpriteNode, "pixel_size", 0.00, 0.4);
        await ToSignal(tween, "finished");

        CountryState.ForId(countryId).Node.RemoveUnit(this);
    }

    private void OnAfterUnitRemovedFromCountry()
    {
        // Hook, no action needed
    }

    private void OnClickableSprite3DMouseLeftClickOpaque(ClickableSprite3D sprite)
    {
        if (clickable && clickCallback.Delegate != null)
        {
            clickCallback.Call(this);
        }
    }
}
