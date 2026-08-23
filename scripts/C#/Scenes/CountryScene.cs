using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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
    public ClickableSprite CountrySprite => GetNode<ClickableSprite>("CountryClickableSprite");

    public MarginContainer CountrySpriteTextureRectContainer => GetNode<MarginContainer>("%TextureRectContainer");
    public OpaqueTextureRect CountrySpriteTextureRect => GetNode<OpaqueTextureRect>("%TextureRect");
    public MarginContainer TVCountrySpriteTextureRectContainer => GetNode<MarginContainer>("%TVTextureRectContainer");
    public OpaqueTextureRect TVCountrySpriteTextureRect => GetNode<OpaqueTextureRect>("%TVTextureRect");


    public Label CountryLabel => GetNode<Label>("CountryLabel");
    public static readonly PackedScene CountryScenePacked = GD.Load<PackedScene>("res://scenes/World/Country.tscn");

    public Vector2 Size => new Vector2(this.CountryState.StaticCountryData.Texture.GetWidth(), this.CountryState.StaticCountryData.Texture.GetHeight());
    public Rect2 Bounds => new Rect2(this.Position - (Size/2), Size);

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
        countrySceneInstance.Position = countrySceneInstance.StaticCountryData.WorldPositionCenter;
        countrySceneInstance.CountryLabel.Text = countrySceneInstance.StaticCountryData.Label;
        
        countrySceneInstance.CountryLabel.Size = countrySceneInstance.Bounds.Size;
        Vector2 countryTopLeftPos = -(countrySceneInstance.Bounds.Size/2);
        Vector2 labelPos = countryTopLeftPos + countrySceneInstance.StaticCountryData.LabelTransformData.Position2D;

        countrySceneInstance.CountryLabel.Position = labelPos;
        countrySceneInstance.CountrySpriteTextureRect.Texture = countrySceneInstance.StaticCountryData.Texture;
        countrySceneInstance.CountrySpriteTextureRectContainer.Size = countrySceneInstance.Bounds.Size;
        countrySceneInstance.CountrySpriteTextureRectContainer.Position = countryTopLeftPos;      
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
        TVCountrySpriteTextureRect.Texture = StaticCountryData.Texture;
        TVCountrySpriteTextureRectContainer.Size = Bounds.Size;
        TVCountrySpriteTextureRectContainer.Position = -(Bounds.Size/2);

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
        defaultTargetScale = CountrySprite.Scale;
        DebugUtilities.PrintPeer($"{this.StaticCountryData.Label}");
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
    }

    private void OnMouseEnteredOpaque() => SetCountryColor(Colors.Green);
    private void OnMouseExitedOpaque() => SetCountryColor(CountryState.Tags.Has(Tag.RebuildTarget, Faction.ALL) ? Colors.Yellow : Colors.Red);

    private void OnMouseLeftClickOpaque()
    {
        EventBus.Emit(EventBus.SignalName.CountryClicked, this.CountryState.Id);
    }


    private void ApplyTexture()
    {
        CountrySprite.SetTexture(AssetRepository.TargetCountrySprite);
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

    public void SetClickable()
    {
        if(targetPresentationMode == TargetPresentationMode.Glow)
        {
            CountrySpriteTextureRect.Visible = true;
            CountrySpriteTextureRectContainer.Visible = true;
            CountrySpriteTextureRect.MouseLeftClickOnOpaque += OnMouseLeftClickOpaque;
            OnMouseExitedOpaque();
        }
        else if(targetPresentationMode == TargetPresentationMode.TargetSprite)
        {
            CountrySprite.Position = new Vector2(0,0) + StaticCountryData.LabelTransformData.Position2D;
            CountrySprite.ShowSprite();

            // A country the asked faction already occupies is a rebuild-in-place target: legal, but the rare
            // option. This marker is the one doing the visual talking — it is authored larger than the unit
            // marker and sits at the country label — so subduing only the unit's marker left the target
            // looking exactly like an ordinary one. Both are subdued now.
            if (CountryState.Tags.Has(Tag.RebuildTarget, Faction.ALL))
            {
                CountrySprite.Scale = defaultTargetScale * SubduedTargetScaleFactor;
                CountrySprite.SetClickableSubdued();
                return;
            }
            CountrySprite.Scale = defaultTargetScale;
            CountrySprite.SetClickable();
        }
    }

    public void SetUnclickable()
    {
        CountrySprite.Scale = defaultTargetScale;
        CountrySprite.HideSprite();
        CountrySprite.SetUnclickable();

        CountrySpriteTextureRect.Visible = false;
        CountrySpriteTextureRectContainer.Visible = false;
        CountrySpriteTextureRect.MouseLeftClickOnOpaque -= OnMouseLeftClickOpaque;
        OnMouseExitedOpaque();
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
