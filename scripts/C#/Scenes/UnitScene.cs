using Godot;
using System;

public partial class UnitScene : Node2D
{   
	public int UnitId { get; set; }
	public UnitState UnitState => UnitState.ForId(UnitId);
	public Sprite2D UnitSpriteNode => GetNode<Sprite2D>("UnitSprite2D");
	public ClickableTextureRect TargetSprite => GetNode<ClickableTextureRect>("TargetSprite");
	public TextureRect OutOfSupplyNode => GetNode<TextureRect>("OutOfSupplyIcon");
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

	/// <summary>
	/// How much smaller a rebuild-in-place target is drawn than an ordinary one. Well under half: the
	/// country's own full-size marker is still on screen beside it, and at 0.6 the two read as the same
	/// kind of target.
	/// </summary>
	private const float SubduedTargetScaleFactor = 0.45f;

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

		// Units are all spawned up front and only later deployed, so a fresh scene is a pool unit
		// sitting on the same off-board spot a removed one is parked on — same rule, same treatment.
		if (!UnitState.IsDeployedToCountry) ResetToPoolState();
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
		UpdateMarkerVisibilityLayer();
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
		UpdateMarkerVisibilityLayer();
		TargetSprite.Scale = defaultTargetScale * SubduedTargetScaleFactor;
		TargetSprite.ShowSprite();
		TargetSprite.SetClickableSubdued();
	}

	/// <summary>True while this unit is an actually-offered selection target.</summary>
	private bool IsOfferedTarget =>
		UnitState.Tags.Has(Tag.Clickable, Faction.ALL) || UnitState.Tags.Has(Tag.RebuildTarget, Faction.ALL);

	/// <summary>True while the player is hovering a card that could affect this unit.</summary>
	private bool IsPreviewTarget => UnitState.Tags.Has(Tag.PreviewTarget, Faction.ALL);

	/// <summary>True while the focus viewport is pointed at the country this unit stands in.</summary>
	private bool IsFocusTarget => UnitState.Tags.Has(Tag.FocusTarget, Faction.ALL);

	/// <summary>
	/// Which viewports draw the marker. The main board wins wherever it has a reason of its own — an
	/// offered target or a card hover preview — so a unit that is both that and a focus target stays
	/// visible on the board instead of disappearing into the focus view.
	/// </summary>
	private void UpdateMarkerVisibilityLayer()
	{
		TargetSprite.VisibilityLayer = IsOfferedTarget || IsPreviewTarget
			? WorldMirrorViewport.WorldLayer
			: WorldMirrorViewport.FocusOnlyLayer;
	}

	/// <summary>
	/// Show the marker if ANY of the three reasons wants it, and put it in the right viewports.
	///
	/// One resolver rather than each reason showing and hiding for itself. Offered (the selection
	/// handlers), card hover preview (CardTargetPreviewDisplay) and focus view (FocusTargetDisplay) are
	/// raised by parties that know nothing about each other and can overlap in either order, and every
	/// pairwise "don't put away what the other one wants" guard was a bug waiting for the third —
	/// which the focus view now is.
	///
	/// Reads the tags rather than taking a bool: the tag handlers dispatch through CallDeferred, so by
	/// the time this runs the tag state is the authority and a parameter captured earlier could be stale.
	/// </summary>
	private void RefreshTargetMarker()
	{
		UpdateMarkerVisibilityLayer();

		// An offered target owns its own styling — SetClickable and SetRebuildTarget draw the ordinary
		// and the subdued variant, and a preview restyle would flatten the difference.
		if (IsOfferedTarget) return;

		TargetSprite.Scale = defaultTargetScale;

		// A unit off the board shows nothing whatever tags are still on it. A focus mark raised in a
		// block window outlives the removal that window was about, and ResetToPoolState comes through
		// here — without this it would bring a pooled unit's marker back with it.
		if (UnitState.IsDeployedToCountry && (IsPreviewTarget || IsFocusTarget))
		{
			TargetSprite.ShowSprite();
			TargetSprite.SetPreviewOnly();
			return;
		}

		TargetSprite.HideSprite();
		TargetSprite.SetUnclickable();
	}

	/// <summary>Draws (or clears) the card hover preview. <see cref="RefreshTargetMarker"/> decides.</summary>
	public void SetPreviewTarget() => RefreshTargetMarker();

	/// <summary>Draws (or clears) the focus-view indicator. <see cref="RefreshTargetMarker"/> decides.</summary>
	public void SetFocusTarget() => RefreshTargetMarker();

	public void SetUnclickable() => RefreshTargetMarker();

	public void ShowOutOfSupply() => OutOfSupplyNode.Show();
	public void HideOutOfSupply() => OutOfSupplyNode.Hide();

	/// <summary>
	/// The look of a unit sitting in the pool. CountryScene.RemoveUnit parks a removed unit at a fixed
	/// off-board position, so it must not be drawn there, and its transform is put back to what
	/// Unit.tscn authors: the removal tween ends on a zeroed sprite scale, and the next deploy tween
	/// starts from whatever it finds, so without this the redeployed unit pops out of nothing.
	/// The unit is shown again by CountryScene.AddUnit.
	/// </summary>
	public void ResetToPoolState()
	{
		Hide();
		Rotation = 0f;
		Scale = Vector2.One;
		UnitSpriteNode.Rotation = 0f;
		UnitSpriteNode.Scale = new Vector2(DefaultSpriteScale, DefaultSpriteScale);
		SetUnclickable();
		HideOutOfSupply();
	}

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
		else if (tag == Tag.PreviewTarget)
		{
			Callable.From(SetPreviewTarget).CallDeferred();
		}
		else if (tag == Tag.FocusTarget)
		{
			Callable.From(SetFocusTarget).CallDeferred();
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
		else if (tag == Tag.PreviewTarget)
		{
			Callable.From(SetPreviewTarget).CallDeferred();
		}
		else if (tag == Tag.FocusTarget)
		{
			Callable.From(SetFocusTarget).CallDeferred();
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
