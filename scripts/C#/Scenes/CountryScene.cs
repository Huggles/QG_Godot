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
    public Label CountryLabel => GetNode<Label>("CountryLabel");

    public static readonly PackedScene CountryScenePacked = GD.Load<PackedScene>("res://scenes/World/Country.tscn");

    public Vector2 Size => new Vector2(this.CountryState.StaticCountryData.Texture.GetWidth(), this.CountryState.StaticCountryData.Texture.GetHeight());
    public Rect2 Bounds => new Rect2(this.Position - (Size/2), Size);

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

        

        countrySceneInstance.CountryLabel.Position = new Vector2(
            - (countrySceneInstance.CountryLabel.Size.X / 2), 
            - (countrySceneInstance.CountryLabel.Size.Y / 2)
        ) + countrySceneInstance.StaticCountryData.LabelTransformData.Position2D;
        
        return countrySceneInstance;
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
        EventBus.Instance.CountryNamesToggled += OnCountryNameToggled;
    }

    public override void _ExitTree()
    {
        CountryState.Tags.TagAdded -= OnTagAdded;
        CountryState.Tags.TagRemoved -= OnTagRemoved;
        EventBus.Instance.CountryNamesToggled -= OnCountryNameToggled;
    }

    private void OnCountryNameToggled(bool show)
    {
        CountryLabel.Visible = show;
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

    private void OnMouseLeftClickOpaque()
    {
        EventBus.Emit(EventBus.SignalName.CountryClicked, this.CountryState.Id);
    }


    private void ApplyTexture()
    {
        CountrySprite.SetTexture(AssetRepository.TargetCountrySprite);
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
    }

    public void SetClickable()
    {
        CountrySprite.Position =
        new Vector2(0,0)
        + StaticCountryData.LabelTransformData.Position2D;
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

    public void SetUnclickable()
    {
        CountrySprite.Scale = defaultTargetScale;
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
