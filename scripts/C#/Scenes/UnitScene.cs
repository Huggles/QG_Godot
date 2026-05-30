using Godot;
using System;

public partial class UnitScene : Node2D
{   
    public int UnitId { get; set; }
    public UnitState UnitState => UnitState.ForId(UnitId);

    public Sprite2D UnitSpriteNode => GetNode<Sprite2D>("UnitSprite2D");
    public ClickableSprite TargetSprite => GetNode<ClickableSprite>("TargetSprite");
    public Sprite2D OutOfSupplyNode => GetNode<Sprite2D>("OutOfSupplyIcon");

    
    // Signals
    [Signal] public delegate void UnitClickedEventHandler();

    [Signal] public delegate void UnitDoubleClickedEventHandler();

    // Properties
    public bool IsArmy => UnitType == UnitType.ARMY;
    public bool IsNavy => UnitType == UnitType.NAVY;

    public UnitType UnitType => UnitState.Type;
    public Faction Faction => UnitState.Faction;
    public FactionData FactionData => StaticGameData.FactionDataMap[Faction];
    public CountryState CountryState => UnitState.CountryState;

    // Factory
    public static UnitScene SpawnUnit(int unitId)
    {
        UnitScene unitSceneInstance = AssetRepository.UnitScenePacked.Instantiate<UnitScene>();
        unitSceneInstance.UnitId = unitId;

        var factionData = StaticGameData.FactionDataMap[unitSceneInstance.Faction];
        unitSceneInstance.Name = $"{factionData.UniqueName}_{unitSceneInstance.UnitType}_{unitId}";
        
        // Subscribe to tag events for visual updates
        unitSceneInstance.UnitState.Tags.TagAdded += unitSceneInstance.OnTagAdded;
        unitSceneInstance.UnitState.Tags.TagRemoved += unitSceneInstance.OnTagRemoved;
        NodeUtilities.Instance.UnitsNode.AddChild(unitSceneInstance, true);

        return unitSceneInstance;
    }

    public override void _Ready()
    {
        EventBus.Instance.UnitDeployed += OnUnitDeployedToCountry;
        EventBus.Instance.UnitRemoved += OnUnitRemovedFromCountry;

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
        UnitSpriteNode.Texture = IsArmy ? AssetRepository.ArmySprite : AssetRepository.NavySprite;
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

    private void OnTagAdded(Tag tag, Faction faction)
    {
        if (tag == Tag.Clickable)
        {
            SetClickable();
        }
        else if (tag == Tag.InSupply)
        {
            HideOutOfSupply();
        }
    }

    private void OnTagRemoved(Tag tag, Faction faction)
    {
        if (tag == Tag.Clickable)
        {
            SetUnclickable();
        }
        else if (tag == Tag.InSupply)
        {
            ShowOutOfSupply();
        }
    }

    private async void OnUnitDeployedToCountry(int unitId, int countryId)
    {
        if(unitId != this.UnitId)
            return;
        CountryState.CountryScene.AddUnit(this);
        Tween tween = CreateTween();
        tween.TweenProperty(UnitSpriteNode, "scale", new Vector2(1.5f, 1.5f), GameSettings.AnimationDurationSeconds/2);
        tween.TweenProperty(UnitSpriteNode, "scale", new Vector2(1, 1), GameSettings.AnimationDurationSeconds/2);
        await ToSignal(tween, "finished");
    }

    private async void OnUnitRemovedFromCountry(int unitId, int countryId)
    {
        if(unitId != this.UnitId)
            return;
        Tween tween = CreateTween(); 
        tween.TweenProperty(UnitSpriteNode, "scale", new Vector2(1.2f, 1.2f), GameSettings.AnimationDurationSeconds/2);
        tween.TweenProperty(UnitSpriteNode, "scale", new Vector2(0,0), GameSettings.AnimationDurationSeconds/2);
        await ToSignal(tween, "finished");

        CountryState.ForId(countryId).CountryScene.RemoveUnit(this);
    }

    private void OnClickableSprite3DMouseLeftClickOpaque(ClickableSprite3D sprite)
    {
        EventBus.Emit(EventBus.SignalName.UnitClicked, this.UnitState.Id);
    }
}
