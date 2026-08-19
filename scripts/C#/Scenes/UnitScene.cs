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

	/// <summary>
	/// TargetSprite's authored scale, captured before anything shrinks it, so the subdued mode is
	/// defined relative to whatever Unit.tscn says rather than to a duplicated constant.
	/// </summary>
	private Vector2 defaultTargetScale;

	/// <summary>How much smaller a rebuild-in-place target is drawn than an ordinary one.</summary>
	private const float SubduedTargetScaleFactor = 0.6f;

	public override void _Ready()
	{
		defaultTargetScale = TargetSprite.Scale;
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
	/// <summary>
	/// One sprite, two answers. Ordinarily the target on a unit means "pick this unit". When it is the
	/// rebuild-in-place marker instead, the thing being chosen is the COUNTRY the unit stands on — the
	/// deploy targets a space, and the unit is only how that space is pointed at while occupied.
	///
	/// Read off the tags at click time rather than cached in a field: both tags are raised and cleared
	/// by the selection handlers, and a cached flag is one more thing that can be left stale by a
	/// handler that unwinds. Tag.Clickable wins if somehow both are set — being asked for a unit is the
	/// more specific request.
	/// </summary>
	private void OnMouseLeftClickOpaque()
	{
		if (UnitState.Tags.Has(Tag.Clickable, Faction.ALL))
		{
			EventBus.Emit(EventBus.SignalName.UnitClicked, this.UnitState.Id);
			return;
		}

		if (UnitState.Tags.Has(Tag.RebuildTarget, Faction.ALL))
		{
			EventBus.Emit(EventBus.SignalName.CountryClicked, this.UnitState.CountryId);
		}
	}

	private void SetSprite()
	{
		UnitSpriteNode.Texture = IsArmy ? AssetRepository.ArmySprite : AssetRepository.NavySprite;
		UnitSpriteNode.Modulate = FactionData.FactionColor;
	}

	public void SetClickable()
	{
		TargetSprite.Scale = defaultTargetScale;
		TargetSprite.ShowSprite();
		TargetSprite.SetClickable();
	}

	/// <summary>
	/// Mark this unit as the rebuild-in-place deploy target for the country it stands on: smaller and
	/// fainter than an ordinary target, because being able to build onto a space you already hold is
	/// the rare option and must not read as loudly as the ordinary ones beside it.
	/// </summary>
	public void SetRebuildTarget()
	{
		TargetSprite.Scale = defaultTargetScale * SubduedTargetScaleFactor;
		TargetSprite.ShowSprite();
		TargetSprite.SetClickableSubdued();
	}

	public void SetUnclickable()
	{
		TargetSprite.Scale = defaultTargetScale;
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
		else if (tag == Tag.RebuildTarget)
		{
			Callable.From(SetRebuildTarget).CallDeferred();
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
		if (tag == Tag.Clickable || tag == Tag.RebuildTarget)
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
