using Godot;
using System;

public partial class UnitScene : Node2D
{   
	public int UnitId { get; set; }
	public UnitState UnitState => UnitState.ForId(UnitId);
	public Sprite2D UnitSpriteNode => GetNode<Sprite2D>("UnitSprite2D");
	public ClickableSprite TargetSprite => GetNode<ClickableSprite>("TargetSprite");
	public BlinkingSprite2D OutOfSupplyNode => GetNode<BlinkingSprite2D>("OutOfSupplyIcon");
	public static float DefaultSpriteScale = 0.2f;    
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
		unitSceneInstance.UnitState.UnitScene = unitSceneInstance; // set reference for easy access in animations
		NodeUtilities.Instance.UnitsNode.AddChild(unitSceneInstance, true);

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

	public void ShowOutOfSupply() => OutOfSupplyNode.Show();
	public void HideOutOfSupply() => OutOfSupplyNode.Hide();

	private void OnTagAdded(Tag tag, Faction faction)
	{
		if (tag == Tag.Clickable)
		{
			Callable.From(SetClickable).CallDeferred();
		}
		else if (tag == Tag.InSupply || tag == Tag.SuppliedForTurn)
		{
			Callable.From(HideOutOfSupply).CallDeferred();
		}
		else if (tag == Tag.OutOfSupply || tag == Tag.SuppliedForTurn)
		{
			Callable.From(ShowOutOfSupply).CallDeferred();
		}
	}

	private void OnTagRemoved(Tag tag, Faction faction)
	{
		if (tag == Tag.Clickable)
		{
			Callable.From(SetUnclickable).CallDeferred();
		}
		else if ((tag == Tag.InSupply || tag == Tag.SuppliedForTurn) && !UnitState.InSupply)
		{
			Callable.From(ShowOutOfSupply).CallDeferred();
		}
		else if ((tag == Tag.OutOfSupply || tag == Tag.SuppliedForTurn) && UnitState.InSupply)
		{
			Callable.From(HideOutOfSupply).CallDeferred();
		}
		
	}

	private void OnClickableSprite3DMouseLeftClickOpaque(ClickableSprite3D sprite)
	{
		EventBus.Emit(EventBus.SignalName.UnitClicked, this.UnitState.Id);
	}
}
