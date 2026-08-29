using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CountryScene : Control
{
	public int CountryId { get; set; }
	private CountryState CountryState => CountryState.ForId(CountryId);

	public CountryData StaticCountryData => CountryState.StaticCountryData;
	public StraightState StraightState => CountryState.StraightState;

	public UnitScene UnitScene1;
	public UnitScene UnitScene2;
	public UnitScene UnitScene3;

	public Control UnitContainerNode => GetNode<Control>("UnitContainer");
	public TextureRect SupplyStarSprite => GetNode<TextureRect>("SupplyStarSprite");
	public TextureRect StraightSpriteNode => GetNode<TextureRect>("StraightSprite");

	public MarginContainer CountrySpriteTextureRectContainer => GetNode<MarginContainer>("%TextureRectContainer");
	public OpaqueTextureRect CountrySpriteTextureRect => GetNode<OpaqueTextureRect>("%TextureRect");
	public MarginContainer TVCountrySpriteTextureRectContainer => GetNode<MarginContainer>("%TVTextureRectContainer");
	public OpaqueTextureRect TVCountrySpriteTextureRect => GetNode<OpaqueTextureRect>("%TVTextureRect");


	public Label CountryLabel => GetNode<Label>("CountryLabel");
	public static readonly PackedScene CountryScenePacked = GD.Load<PackedScene>("res://scenes/World/Country.tscn");

	public Vector2 TextureSize => new Vector2(this.CountryState.StaticCountryData.Texture.GetWidth(), this.CountryState.StaticCountryData.Texture.GetHeight());
	/// <summary>The country's rect in the Countries node's space. Everything it owns sits inside this.</summary>
	public Rect2 Bounds => new Rect2(Position, Size);

	/// <summary>
	/// Middle of the country. A Control's Position is its top-left corner, so anything aiming at "the
	/// country" — the camera, the trigger preview — wants this rather than GlobalPosition.
	/// </summary>
	public Vector2 GlobalCenter => GlobalPosition + (Size / 2f);

	public TargetPresentationMode targetPresentationMode = TargetPresentationMode.Glow;

	public WorldPresentationMode worldPresentationMode
	{
		get {
			return field;
		}
		set {
			field = value;
			// Pass the value through. Calling this argument-less took the parameter default instead, so
			// every assignment — Tactical included — landed on the Normal branch.
			ChangeWorldPresentationMode(value);
		}
	}    

	public enum TargetPresentationMode
	{
		Glow,
		TargetSprite
	}


	public static CountryScene SpawnCountry(int countryId)
	{
		CountryScene countrySceneInstance = CountryScenePacked.Instantiate<CountryScene>();                
		countrySceneInstance.CountryId = countryId;        
		countrySceneInstance.CountryState.Tags.TagAdded += countrySceneInstance.OnTagAdded;
		countrySceneInstance.CountryState.Tags.TagRemoved += countrySceneInstance.OnTagRemoved;
		NodeUtilities.Instance.CountriesNode.AddChild(countrySceneInstance, false);

		countrySceneInstance.CountryLabel.Text = countrySceneInstance.StaticCountryData.Label;

		// The label fills the country (anchors preset 15) and is nudged in from the top-left corner by
		// the authored offset. Written as offsets rather than Position/Size: with stretching anchors
		// those two are derived from the parent's rect, so assigning them is both overridden after
		// _ready and warned about ("non-equal opposite anchors").
		Vector2 labelOffset = countrySceneInstance.StaticCountryData.LabelTransformData?.Position2D ?? Vector2.Zero;
		countrySceneInstance.CountryLabel.OffsetLeft = labelOffset.X;
		countrySceneInstance.CountryLabel.OffsetTop = labelOffset.Y;

		countrySceneInstance.CountrySpriteTextureRect.Texture = countrySceneInstance.StaticCountryData.Texture;
		countrySceneInstance.TVCountrySpriteTextureRect.Texture = countrySceneInstance.StaticCountryData.Texture;

		// Only the country itself gets a rect. Every child is anchored to it, so they follow along and
		// stay inside these bounds without a single position of their own. The authored position is
		// the top-left corner, so it goes straight in.
		countrySceneInstance.Size = countrySceneInstance.TextureSize;
		countrySceneInstance.Position = countrySceneInstance.StaticCountryData.WorldPositionTopLeft;

		countrySceneInstance.CountrySpriteTextureRectContainer.Visible = false;
		return countrySceneInstance;
	}

	private void ChangeWorldPresentationMode(WorldPresentationMode newMode = WorldPresentationMode.Normal)
	{
		switch (newMode)
		{
			case WorldPresentationMode.Normal:
				ShowWorldPresentationNormal();
				break;
			case WorldPresentationMode.Tactical:
			// Same overlay, different palette — UpdateOccupyingFactionColors reads worldPresentationMode
			// to decide whether it paints factions or sides, so both views set up identically here.
			case WorldPresentationMode.TacticalTeam:
				ShowWorldPresentationTactical();
				break;
		}        
	}

	private void ShowWorldPresentationNormal()
	{
		// Was empty, which was harmless only while nothing ever reached the Tactical branch. Now that the
		// signal does, switching back has to put the overlay away or the stripes stay on screen for good.
		TVCountrySpriteTextureRectContainer.Visible = false;
	}

	private void ShowWorldPresentationTactical()
	{
		// The overlay is only repainted when a unit moves, so a country whose garrison has not changed
		// since the last time tactical mode was off would come back with a stale palette.
		UpdateOccupyingFactionColors();
		TVCountrySpriteTextureRectContainer.Visible = true;
	}

	/// <summary>
	/// CountrySprite's authored scale, captured before anything shrinks it, so the subdued rebuild
	/// style is defined relative to whatever Country.tscn says.
	/// </summary>
	private Vector2 defaultTargetScale;

	/// <summary>
	/// How much smaller a rebuild-in-place target is drawn than an ordinary one. Matches
	/// UnitScene.SubduedTargetScaleFactor so the two markers for the same space shrink together.
	/// </summary>
	private const float SubduedTargetScaleFactor = 0.45f;

	public override void _Ready()
	{
		DebugUtilities.PrintPeer($"{this.StaticCountryData.Label}");
		Name = CountryState.Name;
		//SetUnclickable();
		if (CountryState.IsSupply)
			ShowSupplyStar();
		else
			HideSupplyStar();

		SetUnclickable();
		
		StraightState.OnReady();

		CountryLabel.Visible = GameSettings.ShowCountryLabels;

		EventBus.Instance.WorldPresentationViewChanged += OnWorldPresentationViewChanged;
		EventBus.Instance.CountryNamesToggled += OnCountryNameToggled;

		CountrySpriteTextureRect.MouseEntered += OnMouseEnteredOpaque;
		CountrySpriteTextureRect.MouseExited += OnMouseExitedOpaque;
	}

	public override void _ExitTree()
	{
		CountryState.Tags.TagAdded -= OnTagAdded;
		CountryState.Tags.TagRemoved -= OnTagRemoved;
		EventBus.Instance.WorldPresentationViewChanged -= OnWorldPresentationViewChanged;
		EventBus.Instance.CountryNamesToggled -= OnCountryNameToggled;
	}

	private void OnCountryNameToggled(bool show)
	{
		CountryLabel.Visible = show;
	}

	/// <summary>
	/// Every country listens for itself: the view is a property of the whole map, but each CountryScene
	/// owns its own overlay nodes and duplicated stripe material, so there is nothing central to switch.
	/// Assigning through the property rather than calling ChangeWorldPresentationMode directly keeps
	/// worldPresentationMode readable as "what this country is currently drawing".
	/// </summary>
	private void OnWorldPresentationViewChanged(WorldPresentationMode mode)
	{
		worldPresentationMode = mode;
	}

	/// <summary>
	/// The ONLY listener for the tags that drive this country's target visuals. Tag.Clickable was
	/// once handled here and in the CountryState constructor at the same time, which subscribed the
	/// click handler twice; keep every new visual tag in this one place.
	/// </summary>
	private void OnTagAdded(Tag tag, Faction faction)
	{
		if (tag == Tag.Clickable)
		{
			SetClickable();
		}
		// Restyle rather than assume an order: SelectCountryHandler raises RebuildTarget before
		// Clickable, but a caller that ever does it the other way round would otherwise leave the
		// country drawn as an ordinary target.
		else if (tag == Tag.RebuildTarget && CountryState.Tags.Has(Tag.Clickable, Faction.ALL))
		{
			SetClickable();
		}
		else if (tag == Tag.PreviewTarget)
		{
			SetPreviewTarget(true);
		}
	}

	private void OnTagRemoved(Tag tag, Faction faction)
	{
		if (tag == Tag.Clickable)
		{
			SetUnclickable();
		}
		// Still an offered target, just no longer a rebuild — back to the ordinary style.
		else if (tag == Tag.RebuildTarget && CountryState.Tags.Has(Tag.Clickable, Faction.ALL))
		{
			SetClickable();
		}
		else if (tag == Tag.PreviewTarget)
		{
			SetPreviewTarget(false);
		}
	}

	/// <summary>
	/// Only an offered target answers the cursor. The same overlay is also drawn for a card hover
	/// preview, which is deliberately NOT clickable — the game is waiting for a card, not a country —
	/// so tinting it said "click me" about a country nothing was asking for.
	///
	/// The exit handler stays ungated: ShowTargetGlow and HideTargetGlow call it directly to set the
	/// resting colour, not only on a real mouse exit.
	/// </summary>
	private void OnMouseEnteredOpaque()
	{
		if (!IsOfferedTarget) return;
		SetCountryColor(Colors.Green);
	}

	private void OnMouseExitedOpaque() => SetCountryColor(CountryState.Tags.Has(Tag.RebuildTarget, Faction.ALL) ? Colors.Yellow : Colors.Red);

	private void OnMouseLeftClickOpaque()
	{
		EventBus.Emit(EventBus.SignalName.CountryClicked, this.CountryState.Id);
	}

	/// <summary>
	/// The TextureRect's glow material, duplicated per country. Country.tscn's ShaderMaterial is a
	/// sub-resource shared by every instance, so recoloring it directly would recolor the whole map.
	/// </summary>
	private ShaderMaterial countryShaderMaterial;

	private ShaderMaterial CountryShaderMaterial
	{
		get
		{
			if (countryShaderMaterial == null)
			{
				countryShaderMaterial = (CountrySpriteTextureRect.Material as ShaderMaterial)?.Duplicate() as ShaderMaterial;
				CountrySpriteTextureRect.Material = countryShaderMaterial;
			}
			return countryShaderMaterial;
		}
	}

	/// <summary>
	/// Tints the country silhouette. <paramref name="color"/> is the outline glow; the fill follows it
	/// unless <paramref name="fillColor"/> names its own. Opacity stays with the material's authored
	/// glow_alpha/fill_alpha, so a caller only has to think about hue.
	/// </summary>
	public void SetCountryColor(Color color, Color? fillColor = null)
	{
		ShaderMaterial material = CountryShaderMaterial;
		if (material == null) return;

		material.SetShaderParameter("glow_color", color);
		material.SetShaderParameter("fill_color", fillColor ?? color);
	}


	/// <summary>
	/// The tactical overlay's stripe material, duplicated per country for the same reason as
	/// <see cref="CountryShaderMaterial"/>: StripesMaterial.tres is one shared resource, so writing the
	/// palette straight onto it would give every country on the map the same factions' stripes.
	/// </summary>
	private ShaderMaterial tvCountryShaderMaterial;

	private ShaderMaterial TVCountryShaderMaterial
	{
		get
		{
			if (tvCountryShaderMaterial == null)
			{
				tvCountryShaderMaterial = (TVCountrySpriteTextureRect.Material as ShaderMaterial)?.Duplicate() as ShaderMaterial;
				TVCountrySpriteTextureRect.Material = tvCountryShaderMaterial;
			}
			return tvCountryShaderMaterial;
		}
	}

	/// <summary>
	/// The straight icon's material, duplicated per country for the same reason as
	/// <see cref="CountryShaderMaterial"/>: StraightMaterial.tres is one shared resource, so writing a
	/// team color straight onto it would recolor every straight on the map at once.
	/// </summary>
	private ShaderMaterial straightShaderMaterial;

	private ShaderMaterial StraightShaderMaterial
	{
		get
		{
			if (straightShaderMaterial == null)
			{
				straightShaderMaterial = (StraightSpriteNode.Material as ShaderMaterial)?.Duplicate() as ShaderMaterial;
				StraightSpriteNode.Material = straightShaderMaterial;
			}
			return straightShaderMaterial;
		}
	}

	/// <summary>
	/// Colors the straight icon. <paramref name="fillColor"/> fills the silhouette; the border keeps the
	/// width and color authored on StraightMaterial.tres unless <paramref name="borderColor"/> names its
	/// own, so the outline stays tunable from the inspector rather than hardcoded per team.
	/// </summary>
	public void SetStraightColor(Color fillColor, Color? borderColor = null)
	{
		ShaderMaterial material = StraightShaderMaterial;
		if (material == null) return;

		material.SetShaderParameter("fill_color", fillColor);
		if (borderColor.HasValue)
			material.SetShaderParameter("border_color", borderColor.Value);
	}

	/// <summary>
	/// Stripes.gdshader's MAX_COLORS. GLSL arrays are fixed size, so the `colors` uniform is always
	/// written at exactly this length and `color_count` decides how much of it is read. Godot ignores a
	/// shorter array, which is why <see cref="SetStripeColors"/> pads instead of passing what it has.
	/// </summary>
	private const int MaxStripeColors = 3;

	/// <summary>
	/// What the overlay paints for a country nobody occupies. Fully transparent, because the shader ends
	/// on <c>colors[index] * COLOR</c> — a zero-alpha entry multiplies the silhouette away, so unclaimed
	/// space simply is not drawn rather than showing a placeholder tint over half the map.
	/// </summary>
	private static readonly Color UnoccupiedStripeColor = new Color(0.2f, 0.2f, 0.2f, 1);
	private static readonly Color UnoccupiedStripeColorLand = new Color(0.1f, 0.15f, 0.1f, 1);
	private static readonly Color UnoccupiedStripeColorWater = new Color(0.1f, 0.1f, 0.15f, 1);

	/// <summary>
	/// Writes the stripe palette on the tactical overlay: <paramref name="colors"/> padded out to the
	/// shader's fixed array length into `colors`, and how many of those entries to cycle through into
	/// `color_count`. An empty (or null) list paints the country as unoccupied; anything past
	/// <see cref="MaxStripeColors"/> is dropped, which a country capped at three units cannot reach.
	/// </summary>
	public void SetStripeColors(IReadOnlyList<Color> colors)
	{
		ShaderMaterial material = TVCountryShaderMaterial;
		if (material == null) return;

		int count = Math.Min(colors?.Count ?? 0, MaxStripeColors);
		Color[] padded = new Color[count];
		if(count > 0)
		{            
			for (int i = 0; i < count; i++)
				padded[i] = colors[i];
		} else
		{
			padded = new Color[2];
			padded[0] = UnoccupiedStripeColor;
			padded[1] = this.CountryState.IsLand ? UnoccupiedStripeColorLand : UnoccupiedStripeColorWater;
		}        

		// The shader clamps color_count to at least 1, so an empty palette would read colors[0] whatever
		// we asked for. Say 1 out loud and let it land on the transparent entry padding already put there.
		material.SetShaderParameter("colors", padded);
		material.SetShaderParameter("color_count", Math.Max(padded.Length, 1));
	}

	/// <summary>
	/// The distinct factions with a unit standing here, in slot order.
	///
	/// Read off the UnitScene slots rather than <c>CountryState.Units</c> so the overlay always agrees
	/// with the pieces actually drawn on the board: the state dictionary is updated when the change
	/// event applies, while the scene arrives a deploy animation later.
	/// </summary>
	public List<Faction> OccupyingUnitFactions()
	{
		List<Faction> factions = new List<Faction>();
		foreach (UnitScene unitScene in new[] { UnitScene1, UnitScene2, UnitScene3 })
		{
			if (unitScene == null) continue;
			// Two armies of the same faction are one stripe, not two — the overlay answers "who is here",
			// and a doubled band would only read as a wider stripe anyway.
			if (!factions.Contains(unitScene.Faction)) factions.Add(unitScene.Faction);
		}
		return factions;
	}

	/// <summary>
	/// Repaints the tactical overlay from whoever is standing here — one stripe per occupant, in the color
	/// the current view asks for. Called by <see cref="AddUnit(UnitScene)"/> and <see cref="RemoveUnit"/>,
	/// the only two places the slots change, and again when the country switches into a tactical mode.
	///
	/// Reads worldPresentationMode on every repaint rather than being told which palette to use, so a unit
	/// arriving while TacticalTeam is on does not repaint the country in faction colors.
	/// </summary>
	public void UpdateOccupyingFactionColors()
	{
		SetStripeColors(OccupyingStripeColors());
	}

	/// <summary>
	/// One color per occupant for the current view: the faction's own color in Tactical, its side's color
	/// in TacticalTeam. Teams are de-duplicated the same way factions are in
	/// <see cref="OccupyingUnitFactions"/> — Germany and Italy sharing a country is one Axis stripe, not
	/// two identical red ones. Normal never reaches here, since the overlay it draws is hidden.
	/// </summary>
	private List<Color> OccupyingStripeColors()
	{
		List<Faction> factions = OccupyingUnitFactions();

		if (worldPresentationMode == WorldPresentationMode.TacticalTeam)
		{
			return factions
				.Select(StaticGameData.FactionTeamForFaction)
				.Distinct()
				.Select(StaticGameData.FactionTeamColor)
				.ToList();
		}

		return factions
			.Select(faction => StaticGameData.FactionDataMap[faction].FactionColor)
			.ToList();
	}

	public UnitScene AddUnit(int unitId)
	{
		UnitScene unitScene = UnitState.ForId(unitId).UnitScene;
		if(unitScene == null) throw new Exception($"CountryScene.AddUnit: No UnitScene found for unitId {unitId}.");
		AddUnit(unitScene);
		return unitScene;
	}

	public void AddUnit(UnitScene unitScene)
	{
		int position = SetUnitOnAvailablePosition(unitScene);
		if (position <= 0) return;

		// Ahead of every early return below: the slot is booked by this point, so the overlay must
		// reflect the new occupant even on the rebuild-in-place path that skips the re-parenting.
		UpdateOccupyingFactionColors();

		// Undoes the Hide() from RemoveUnit. Ahead of the already-parented early return below, because
		// a rebuild-in-place unit is on the board either way and must be drawn.
		unitScene.Show();

		// A "build that army again" redeploys the piece already standing here, so DeployUnitAnimation
		// calls this for a scene that never left. Re-parenting it to the node it is already under would
		// only churn child order; SetUnitOnAvailablePosition has already returned the slot it holds, so
		// the tween still plays on the right unit and the slot is not double-booked.
		if (unitScene.GetParent() == UnitContainerNode) return;

		unitScene.GetParent().RemoveChild(unitScene);
		UnitContainerNode.AddChild(unitScene);

		UnitTransformData transformData = StaticCountryData.UnitTransformData;
		if (transformData == null) return;

		var key = $"Position{position}";
		TransformData transform = (TransformData)transformData.GetValue(key);

		// Slot offsets are measured from the middle of the country. UnitContainer used to be a Node2D
		// sitting on that middle; as a Control its origin is the country's top-left corner, so the
		// half-size has to be added back or every unit is drawn a corner away from its slot.
		unitScene.Position = (TextureSize / 2f) + new Vector2(transform.XPosition, transform.YPosition);
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
		// Already holding a slot here — a rebuild in place. Hand back the slot it has rather than
		// booking it a second one, which would leave the same scene registered twice and leak the
		// first slot when the unit is later removed.
		int existing = GetUnitPosition(unitScene);
		if (existing > 0) return existing;

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

		// Off the board is off the screen: every removed unit is parked on the same spot, so leaving
		// them drawn there stacks the whole pool on one pile. ResetToPoolState hides it and puts the
		// transform back to Unit.tscn's, undoing the removal tween. AddUnit shows it again.
		unitScene.ResetToPoolState();

		switch (position)
		{
			case 1: UnitScene1 = null; break;
			case 2: UnitScene2 = null; break;
			case 3: UnitScene3 = null; break;
		}

		// After the slot is cleared, not before: the departing faction must be gone from the palette.
		UpdateOccupyingFactionColors();
	}

	/// <summary>True while this country is an actually-offered selection target.</summary>
	private bool IsOfferedTarget => CountryState.Tags.Has(Tag.Clickable, Faction.ALL);

	/// <summary>True while the player is hovering a card that could affect this country.</summary>
	private bool IsPreviewTarget => CountryState.Tags.Has(Tag.PreviewTarget, Faction.ALL);

	/// <summary>
	/// Shows the glow overlay, without touching the click handler. Split out of
	/// <see cref="SetClickable"/> so the hover preview (<see cref="SetPreviewTarget"/>) can draw the
	/// identical marker for a country that is NOT clickable — the game is waiting for a card, not a
	/// country, so the same visual must not carry the same interaction.
	/// </summary>
	private void ShowTargetGlow()
	{
		CountrySpriteTextureRect.Visible = true;
		CountrySpriteTextureRectContainer.Visible = true;
		OnMouseExitedOpaque();
	}

	/// <inheritdoc cref="ShowTargetGlow"/>
	private void HideTargetGlow()
	{
		CountrySpriteTextureRect.Visible = false;
		CountrySpriteTextureRectContainer.Visible = false;
		OnMouseExitedOpaque();
	}

	/// <summary>
	/// Draws (or clears) the hover preview: the same glow an offered target gets, no click handler.
	///
	/// An offered target wins outright — it owns the click handler, and letting a preview clearing
	/// over the top of it call <see cref="HideTargetGlow"/> would blank a live selection prompt.
	/// Only the Glow presentation draws a preview; the TargetSprite marker is authored as an
	/// "click this" affordance and previewing with it would be a lie.
	/// </summary>
	public void SetPreviewTarget(bool isPreviewTarget)
	{
		if (targetPresentationMode != TargetPresentationMode.Glow) return;
		if (IsOfferedTarget) return;

		if (isPreviewTarget)
			ShowTargetGlow();
		else
			HideTargetGlow();
	}

	public void SetClickable()
	{
		if(targetPresentationMode == TargetPresentationMode.Glow)
		{
			ShowTargetGlow();
			// Detach before attaching, so however many times this runs the handler is attached once.
			// A Godot [Signal] compiles to a plain multicast delegate (add => backing += value) with
			// no duplicate detection, so a second += fires the handler a second time — and this is
			// re-entered while already clickable by OnTagAdded's Tag.RebuildTarget restyle, which is
			// written to tolerate either tag order and so is meant to be reachable. Only one -= ever
			// follows, in SetUnclickable. Removing a handler that is not attached is a safe no-op.
			CountrySpriteTextureRect.MouseLeftClickOnOpaque -= OnMouseLeftClickOpaque;
			CountrySpriteTextureRect.MouseLeftClickOnOpaque += OnMouseLeftClickOpaque;
		}        
	}

	public void SetUnclickable()
	{
		CountrySpriteTextureRect.MouseLeftClickOnOpaque -= OnMouseLeftClickOpaque;

		// The hover preview draws this same overlay, so hiding it unconditionally would blank a
		// preview that is still meant to be up. The click handler above always goes.
		if (IsPreviewTarget)
			ShowTargetGlow();
		else
			HideTargetGlow();
	}

	private void ShowSupplyStar()
	{
		SupplyStarSprite.Visible = true;

		if (StaticCountryData.SupplyStarTransformData != null)
		{
			var data = StaticCountryData.SupplyStarTransformData;

			// The star's offset is authored from the middle of the country, and its scale used to be a
			// Sprite2D's — drawn around its own centre. A Control positions and scales from its
			// top-left, so both have to be re-centred or the star lands in the corner at a fraction of
			// the size it should be.
			SupplyStarSprite.PivotOffset = SupplyStarSprite.Size / 2f;
			SupplyStarSprite.Scale = new Vector2(data.Scale, data.Scale);
			SupplyStarSprite.Position = (TextureSize / 2f) - (SupplyStarSprite.Size / 2f) + data.Position2D;
		}
	}

	private void HideSupplyStar()
	{
		SupplyStarSprite.Visible = false;
	}
}
